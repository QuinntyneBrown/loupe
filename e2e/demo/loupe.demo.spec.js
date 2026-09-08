import { test, expect } from '@playwright/test';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { ComparePage } from '../page-objects/compare-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceUploadPage } from '../page-objects/reference-upload-page.js';
import { ReferenceLinkPage } from '../page-objects/reference-link-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';
import { DeletionPage } from '../page-objects/deletion-page.js';
import { showTitleCard, hideTitleCard, showCaption, hideCaption } from './narration.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixture = (name) => readFileSync(path.join(__dirname, 'fixtures', name));

test('loupe demo: the real application against the real backend', async ({ page }, testInfo) => {
  test.setTimeout(8 * 60_000);
  const signIn = new SignInPage(page);
  const myWork = new MyWorkPage(page);
  const upload = new PhotographUploadPage(page);
  const detail = new PhotographDetailPage(page);
  const compare = new ComparePage(page);
  const inspiration = new InspirationPage(page);
  const refUpload = new ReferenceUploadPage(page);
  const refLink = new ReferenceLinkPage(page);
  const refDetail = new ReferenceDetailPage(page);
  const deletion = new DeletionPage(page);

  await page.goto('/');
  await showTitleCard(page, 'Loupe', 'The real Angular application, driving the real Loupe.Api, Loupe.Worker, and PostgreSQL — no mocked services');
  await page.waitForTimeout(4000);
  await hideTitleCard(page);

  // 1. Sign in — real OIDC redirect through the demo identity provider.
  await showCaption(page, 'Signing in', 'A real OpenID Connect authorization-code + PKCE redirect');
  await signIn.openPrivateDestination();
  await signIn.expectSignInRequired();
  await page.waitForTimeout(1500);
  await signIn.continue();
  await page.waitForURL(/localhost:5444\/authorize/);
  await page.waitForTimeout(1200);
  await page.getByRole('button', { name: /Continue as/ }).click();
  await page.waitForURL(/localhost:4200\/my-work/);
  await myWork.expectOpen();
  await page.waitForTimeout(1800);

  // 2. Upload a photograph with a critique brief.
  await showCaption(page, 'Upload a photograph with a critique brief');
  await upload.open();
  await upload.expectOpen();
  await page.waitForTimeout(900);
  await page.getByLabel('Photograph', { exact: true }).setInputFiles({ name: 'harbor-blue-hour.jpg', mimeType: 'image/jpeg', buffer: fixture('harbor-blue-hour.jpg') });
  await upload.fill({
    title: 'Harbor at blue hour',
    intent: 'Show the last blue light over the water before full dark',
    genre: 'landscape',
    experience: 'Intermediate',
  });
  await page.waitForTimeout(1500);
  await upload.save();
  await detail.expectImage('Harbor at blue hour');
  await page.waitForTimeout(1500);

  // 3. Personal notes — distinct from AI-generated text. Saved before the critique
  // request below so its revision bump can't race this save into a conflict.
  await showCaption(page, 'Personal notes stay separate from AI text');
  await detail.focusNotes();
  await detail.editNotes('Try this again once the tide is fully out — more foreground rock exposed.');
  await detail.saveNotes();
  await expect(page.getByRole('region', { name: 'Personal notes', exact: true }).getByRole('status')).toHaveText('Saved', { timeout: 10_000 });
  await page.waitForTimeout(2200);

  // 4. Request a critique — real Loupe.Worker claims the lease and completes it.
  await showCaption(page, 'Request an AI critique', 'A real Loupe.Worker claims the operation lease and produces a structured result');
  await detail.requestCritique();
  await expect(detail.critique().getByRole('heading', { name: 'Strengths', exact: true })).toBeVisible({ timeout: 20_000 });
  await expect(detail.critique()).toContainText('Demo example', { timeout: 20_000 });
  await page.waitForTimeout(1200);
  await detail.critique().scrollIntoViewIfNeeded();
  await page.waitForTimeout(3200);

  // 5. A second photograph, critiqued, to enable comparison.
  await showCaption(page, 'Save a second attempt to compare');
  await detail.returnToLibrary();
  await myWork.expectOpen();
  await page.waitForTimeout(800);
  await upload.open();
  await page.getByLabel('Photograph', { exact: true }).setInputFiles({ name: 'sunrise.jpg', mimeType: 'image/jpeg', buffer: fixture('sunrise.jpg') });
  await upload.fill({ title: 'Harbor at sunrise', intent: 'Same harbor, warm light instead of blue hour', genre: 'landscape', experience: 'Intermediate' });
  await upload.save();
  await detail.expectImage('Harbor at sunrise');
  await detail.requestCritique();
  await expect(detail.critique().getByRole('heading', { name: 'Strengths', exact: true })).toBeVisible({ timeout: 20_000 });
  await page.waitForTimeout(1800);

  // 6. Compare the two attempts side by side.
  await showCaption(page, 'Compare two attempts', 'Both saved critiques, side by side — no new AI job created');
  await detail.returnToLibrary();
  await myWork.compareAttempts();
  await page.waitForTimeout(1000);
  await compare.choose('Harbor at blue hour');
  await page.waitForTimeout(600);
  await compare.choose('Harbor at sunrise');
  await expect(page.getByRole('region', { name: 'First attempt', exact: true })).toBeVisible({ timeout: 10_000 });
  await expect(page.getByRole('region', { name: 'Second attempt', exact: true })).toBeVisible();
  await page.waitForTimeout(3200);

  // 7. Inspiration — save a reference by direct image upload.
  await showCaption(page, 'Save a reference by image upload');
  await page.getByRole('navigation', { name: 'Library', exact: true }).getByRole('link', { name: 'Inspiration', exact: true }).click();
  await inspiration.expectOpen();
  await page.waitForTimeout(900);
  await refUpload.open();
  await refUpload.expectOpen();
  await page.getByLabel('Reference image', { exact: true }).setInputFiles({ name: 'window-light-study.jpg', mimeType: 'image/jpeg', buffer: fixture('window-light-study.jpg') });
  await refUpload.fill({ title: 'Window light study', attribution: 'Personal reference shelf', notes: 'Soft falloff across the face — revisit for portrait work.' });
  await page.waitForTimeout(1200);
  await refUpload.save();
  await expect(page.getByRole('heading', { name: 'Window light study', exact: true })).toBeVisible({ timeout: 15_000 });
  await page.waitForTimeout(2200);

  // 8. Save a reference by URL and request its import — real SSRF-checked admission.
  await showCaption(page, 'Save a reference by URL', 'A real, SSRF-checked outbound fetch is requested — see docs/demo/README.md for why this does not show it reach Completed');
  await page.getByRole('main').getByRole('link', { name: 'Back to Inspiration', exact: true }).click();
  await inspiration.expectOpen();
  await refLink.open();
  await refLink.expectOpen();
  await refLink.fill({ sourceUrl: 'https://upload.wikimedia.org/wikipedia/commons/4/47/PNG_transparency_demonstration_1.png' });
  await page.waitForTimeout(1200);
  await refLink.save();
  await expect(page.getByRole('region', { name: 'Source import', exact: true })).toBeVisible({ timeout: 15_000 });
  await page.waitForTimeout(1000);
  await refDetail.requestImport();
  await page.waitForTimeout(2800);

  // 9. Delete a photograph — real durable deletion and worker cleanup.
  await showCaption(page, 'Delete a photograph', 'A real Loupe.Worker performs durable cleanup');
  await page.getByRole('link', { name: 'My Work', exact: true }).first().click();
  await myWork.expectOpen();
  await myWork.openPhotograph('Harbor at sunrise');
  await detail.deletePhotograph();
  const deleteDialog = page.getByRole('dialog');
  await expect(deleteDialog).toBeVisible();
  await page.waitForTimeout(1200);
  await deleteDialog.getByRole('button', { name: 'Delete photograph', exact: true }).click();
  await deletion.expectPending();
  await page.waitForTimeout(1200);
  await deletion.expectCompleted();
  await page.waitForTimeout(2500);

  await hideCaption(page);
  await showTitleCard(page, 'Everything above ran against the real backend', 'Real PostgreSQL persistence, real worker leases, real image decoding — see docs/demo/README.md');
  await page.waitForTimeout(4000);

  const video = page.video();
  await page.close();
  if (video) {
    mkdirSync(testInfo.outputDir, { recursive: true });
    await video.saveAs(`${testInfo.outputDir}/loupe.webm`);
  }
});
