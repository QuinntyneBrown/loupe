import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// L2-001.4/L2-030.4: Given a failed or unconfirmed upload, retry preserves its
// fields and operation identity, including after the same file is reselected.
for (const lostResponse of [false, true]) {
  test(`L2-001.4/L2-030.4: retry ${lostResponse ? 'a lost success response' : 'a failed transfer'} without duplication`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(0);
    if (lostResponse) work.library.lostUploadResponses = 1;
    else work.library.errors.upload.push('service_unavailable');
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    const upload = new PhotographUploadPage(page);
    await upload.open();
    await upload.chooseImage();
    const draft = { title: 'Morning light', intent: 'Explore shadows', genre: 'Street', experience: 'Intermediate', requestedFeedback: 'Look at the edges' };
    await upload.fill(draft);
    await upload.save();
    await upload.expectFailure();
    await upload.expectDraft(draft);
    await upload.expectFileRetained();
    expect(work.library.photos).toHaveLength(lostResponse ? 1 : 0);
    await upload.chooseImage();
    await upload.retry();
    await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning light');
    expect(work.library.photos).toHaveLength(1);
    expect(work.library.uploadReceipts.size).toBe(1);
  });
}

test('L2-001.4: an unreadable selected file requires reselection and retains the brief', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  work.library.errors.upload.push('file_unavailable');
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.fill({ intent: 'Keep my brief' });
  await upload.save();
  await upload.expectReselectionRequired();
  await upload.expectDraft({ intent: 'Keep my brief' });
  expect(work.library.photos).toHaveLength(0);
  await upload.chooseImage();
  await upload.retry();
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectBriefValues({ intent: 'Keep my brief' });
});

for (const [code, message] of [
  ['unsupported_media', 'This upload was rejected. Choose a still JPEG, PNG, HEIC or WebP image.'],
  ['image_too_large', 'This upload was rejected because it exceeds the upload size limit. Choose an image of 25 MB or less.'],
  ['invalid_image', 'This image could not be decoded within the limits of 100 megapixels and 20,000 pixels per edge. Choose another image.'],
]) {
  test(`L2-039.1: ${code} explains the image problem and preserves entered fields`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(0);
    work.library.errors.upload.push(code);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    const upload = new PhotographUploadPage(page);
    await upload.open();
    await upload.chooseImage();
    await upload.fill({ intent: 'Keep my brief' });
    await upload.save();
    await upload.expectFailure(message);
    await upload.expectDraft({ intent: 'Keep my brief' });
    await upload.chooseImage('Corrected.png');
    await upload.retry();
    await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Corrected');
  });
}

for (const changed of ['title', 'bytes']) {
test(`L2-030.2: changed ${changed} conflicts and restoring the original resolves the saved upload`, async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  work.library.lostUploadResponses = 1;
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.fill({ title: 'Original' });
  await upload.save();
  await upload.expectFailure();
  if (changed === 'title') await upload.fill({ title: 'Changed' });
  else await upload.chooseFile();
  await upload.retry();
  await upload.expectFailure('This retry differs from an earlier upload. Restore the original file and fields, or return to My Work to start a new upload.');
  expect(work.library.photos).toHaveLength(1);
  if (changed === 'title') await upload.fill({ title: 'Original' });
  else await upload.chooseImage();
  await upload.retry();
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Original');
});
}
