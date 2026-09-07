import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// L2-001/043/044/045/046: Given each required viewport, upload errors, transfer
// progress, retry and discard remain accessible, readable and keyboard operable.
const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);

for (const viewport of viewports) {
  test(`L2-001/043/044/045/046: upload recovery at ${viewport.width}x${viewport.height}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const work = new MyWorkPage(page);
    await work.configureCollection(0);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    const upload = new PhotographUploadPage(page);
    await upload.open();
    const draft = { title: 'Morning light', intent: 'Explore shadows', genre: 'Street', experience: 'Intermediate', requestedFeedback: 'Look at the edges' };
    await upload.fill(draft);
    await upload.chooseFile({ name: 'Drawing.svg', mimeType: 'image/svg+xml' });
    await upload.expectFileError('Choose a still JPEG, PNG, HEIC or WebP image.');
    await upload.expectAccessibleUpload();
    await upload.chooseImage();
    await upload.fill({ genre: 'x'.repeat(101) });
    await upload.expectFieldLimit('genre', 100);
    await upload.expectAccessibleUpload();
    await upload.fill({ genre: 'Street' });
    work.library.pause('upload');
    work.library.errors.upload.push('request_failed');
    await upload.save();
    await expect.poll(() => work.library.calls.filter(call => call === 'upload').length).toBe(1);
    await work.library.reportUploadProgress(page, { transferred: 1000, total: 4000 });
    await upload.expectProgress(1000, 4000);
    await upload.expectAccessibleUpload();
    await work.library.reportUploadProgress(page, { transferred: 2000, total: null });
    await upload.expectIndeterminate(2000);
    await upload.expectAccessibleUpload();
    await upload.returnToLibrary();
    await upload.expectPendingWarning();
    await upload.expectDiscardKeyboardAndLayout();
    await upload.keepEditing();
    work.library.release('upload');
    await upload.expectFailure();
    await upload.expectDraft(draft);
    await upload.expectAccessibleUpload();
    await upload.retry();
    await new PhotographDetailPage(page).expectImage('Morning light');
  });
}
