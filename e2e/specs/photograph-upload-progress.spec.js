import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// L2-001.5: Given an in-flight upload, reported transfer bytes drive progress.
// Missing totals remain indeterminate and transferred bytes do not imply a saved item.
test('L2-001.5: transfer progress updates and completion still waits for persistence', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  work.library.pause('upload');
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.save();
  await expect.poll(() => work.library.calls.filter(call => call === 'upload').length).toBe(1);
  await work.library.reportUploadProgress(page, { transferred: 1000, total: 4000 });
  await upload.expectProgress(1000, 4000);
  await work.library.reportUploadProgress(page, { transferred: 3000, total: 4000 });
  await upload.expectProgress(3000, 4000);
  await work.library.reportUploadProgress(page, { transferred: 4000, total: 4000 });
  await upload.expectSaving();
  expect(work.library.photos).toHaveLength(0);
  work.library.release('upload');
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning');
});

test('L2-001.5: a missing total does not invent a percentage', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  work.library.pause('upload');
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.save();
  await expect.poll(() => work.library.calls.filter(call => call === 'upload').length).toBe(1);
  await upload.expectIndeterminate();
  await work.library.reportUploadProgress(page, { transferred: 1000, total: null });
  await upload.expectIndeterminate(1000);
  work.library.release('upload');
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning');
});

test('L2-001.4/.5: retry clears the failed transfer progress', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  work.library.pause('upload');
  work.library.errors.upload.push('request_failed');
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.save();
  await expect.poll(() => work.library.calls.filter(call => call === 'upload').length).toBe(1);
  await work.library.reportUploadProgress(page, { transferred: 3000, total: 4000 });
  await upload.expectProgress(3000, 4000);
  work.library.release('upload');
  await upload.expectFailure();
  await upload.expectNoProgress();
  work.library.pause('upload');
  await upload.retry();
  await expect.poll(() => work.library.calls.filter(call => call === 'upload').length).toBe(2);
  await upload.expectIndeterminate();
  work.library.release('upload');
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning');
});
