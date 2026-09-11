import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';
import { critiqueOperation } from '../fixtures/critique-operation.js';

const statement = 'The left silhouette separates cleanly from the bright background.';
const region = { x: 0.25, y: 0.6, size: 0.2 };
const priorityStatement = 'The bright edge on the right competes with the subject.';
const priorityRegion = { x: 0.75, y: 0.35, size: 0.15 };

async function openEvidence(page, { orientation = 'landscape', hasRegion = true, brokenImage = false, includePriority = false } = {}) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const photo = work.library.photos[0];
  if (orientation === 'portrait') {
    photo.width = 600;
    photo.height = 800;
    photo.imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="600" height="800"><rect width="600" height="800" fill="#e0e0e0"/><path d="M0 800 350 0h100L100 800" fill="#9a9a9a"/></svg>');
  }
  if (brokenImage) photo.imageUrl = 'data:image/png;base64,aW52YWxpZA==';
  photo.previewUrl = photo.imageUrl;
  const critique = savedCritique();
  critique.content.strengths[0].evidence[0] = {
    kind: 'VisibleObservation', statement, exifField: null, exifValue: null,
    region: hasRegion ? region : null,
  };
  if (includePriority) critique.content.improvements[0].evidence = [{
    kind: 'VisibleObservation', statement: priorityStatement, exifField: null,
    exifValue: null, region: priorityRegion,
  }];
  work.library.critiques.set(photo.id, critique);
  work.library.critiqueOperations.set(photo.id, {
    ...critiqueOperation(photo.id, 'Succeeded'), id: critique.operationId,
    completedAt: critique.generatedAt, message: 'Critique saved.',
  });
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectCritiqueText(statement);
  return { detail, work };
}

for (const orientation of ['landscape', 'portrait']) {
  test(`L2-054: ${orientation} evidence follows hover, keyboard, persistent selection and resizing`, async ({ page }, info) => {
    // Given one visual observation with an image region and other evidence without regions.
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const { detail, work } = await openEvidence(page, { orientation });
    await detail.expectNoEvidenceHighlight();

    // Hover and keyboard focus temporarily reveal the same image area.
    await detail.hoverEvidence(statement);
    await detail.expectEvidenceRegion(region);
    await detail.capture(info.outputPath(`evidence-${orientation}-1440.png`));
    await detail.leaveEvidence();
    await detail.expectNoEvidenceHighlight();
    await detail.focusEvidenceWithKeyboard(statement);
    await detail.expectEvidenceRegion(region);
    await detail.blurEvidence();
    await detail.expectNoEvidenceHighlight();

    // Activation persists across pointer leave and responsive image size changes.
    await detail.clickEvidence(statement);
    await detail.leaveEvidence();
    for (const width of [320, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await detail.expectEvidenceRegion(region);
    }
    await detail.clickEvidence(statement);
    await detail.expectNoEvidenceHighlight();
    await detail.clickEvidence(statement);
    await detail.expectEvidenceRegion(region);
    await detail.dismissEvidence();
    await detail.expectNoEvidenceHighlight();

    // Keyboard activation supports the same toggle and Escape dismissal.
    await detail.leaveEvidence();
    await detail.focusEvidenceWithKeyboard(statement);
    await detail.activateEvidenceWithKeyboard();
    await detail.blurEvidence();
    await detail.expectEvidenceRegion(region);
    await detail.dismissEvidence();
    await detail.expectNoEvidenceHighlight();
    work.library.expectReadsOnly();
  });
}

test('L2-054: selecting evidence in a priority improvement switches the highlighted image area', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  const { detail, work } = await openEvidence(page, { includePriority: true });
  // Given a selected strength, when a priority improvement is selected, its own area is pinned.
  await detail.clickEvidence(statement);
  await detail.leaveEvidence();
  await detail.expectEvidenceRegion(region);
  await detail.clickEvidence(priorityStatement);
  await detail.leaveEvidence();
  await detail.expectEvidenceRegion(priorityRegion);
  // Returning to the strength replaces the selected area rather than leaving a stale highlight.
  await detail.clickEvidence(statement);
  await detail.leaveEvidence();
  await detail.expectEvidenceRegion(region);
  await detail.dismissEvidence();
  await detail.expectNoEvidenceHighlight();
  work.library.expectReadsOnly();
});

test.describe('touch evidence selection', () => {
  test.use({ hasTouch: true, viewport: { width: 375, height: 900 } });
  test('L2-054: tap pins an image area and a second tap clears it', async ({ page }) => {
    const { detail, work } = await openEvidence(page);
    await detail.tapEvidence(statement);
    await detail.expectEvidenceRegion(region);
    await detail.tapEvidence(statement);
    await detail.expectNoEvidenceHighlight();
    work.library.expectReadsOnly();
  });
});

test('L2-054: older critique evidence without coordinates stays readable without invented loupes', async ({ page }) => {
  const { detail, work } = await openEvidence(page, { hasRegion: false });
  await detail.expectNoEvidenceLoupes(statement);
  work.library.expectReadsOnly();
});

test('L2-054: image load failure preserves evidence text and removes image loupes', async ({ page }) => {
  const { detail, work } = await openEvidence(page, { brokenImage: true });
  await detail.expectNoEvidenceLoupes(statement);
  work.library.expectReadsOnly();
});
