import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// Acceptance: L2-001.9, L2-043.7. Cancellation stops the client; server commit may race it.
test('confirmed cancellation aborts the client and ignores a late server acknowledgment', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  work.library.pause('upload');
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  const upload = new PhotographUploadPage(page); await upload.open(); await upload.chooseImage();
  await upload.requestCritiqueAfterSaving(); await upload.save();
  await upload.expectProgressState();
  await upload.cancelTransfer(); await upload.expectDiscardChoice(); await upload.keepEditing();
  await upload.expectProgressState();
  expect(work.library.calls).not.toContain('abortUpload');
  await upload.cancelTransfer(); await upload.discardChanges();
  await work.expectOpen(); await upload.expectCanceledNotice();
  await expect.poll(() => work.library.calls.filter(call => call === 'abortUpload').length).toBe(1);
  work.library.release('upload');
  await expect.poll(() => work.library.photos.length).toBe(1);
  await upload.refreshCanceledUpload();
  await work.expectPhotographCard('Morning');
  await work.expectOpen();
  expect(work.library.calls).not.toContain('requestCritique');
  await upload.open(); await upload.expectEmptyDraft();
});
