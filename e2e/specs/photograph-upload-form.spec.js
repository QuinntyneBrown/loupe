import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// Acceptance: L2-001.7, L2-002.5. The mock's controls retain optional text capabilities.
test('mock form offers two actions and optional details with unset defaults', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  const upload = new PhotographUploadPage(page); await upload.open();
  await upload.expectMockForm();
  await upload.chooseImage();
  await upload.toggleFeedback('Composition'); await upload.toggleFeedback('Technical');
  await upload.fill({ title: 'Study', requestedFeedback: 'Look at the edges' });
  await upload.saveOnly();
  await expect.poll(() => work.library.photos.length).toBe(1);
  expect(work.library.photos[0].brief.requestedFeedback).toBe('Technical, Composition\nLook at the edges');
  expect(work.library.calls).not.toContain('requestCritique');
});

test('drop rejects multiple files and accepts a corrected single image without losing context', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  const upload = new PhotographUploadPage(page); await upload.open();
  await upload.fill({ intent: 'Keep my intention' });
  await upload.dropImages(2); await upload.expectFileError('Choose one photograph at a time.');
  expect(work.library.calls).not.toContain('upload');
  await upload.dropImages(1); await upload.saveOnly();
  await expect.poll(() => work.library.photos.length).toBe(1);
  expect(work.library.photos[0].brief.intent).toBe('Keep my intention');
});
