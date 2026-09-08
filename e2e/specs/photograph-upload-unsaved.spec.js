import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// L2-043.5: Given an unfinished upload, navigation protects the draft until an
// explicit discard or sign-out; acknowledged uploads leave without a prompt.
async function openUpload(page) {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  return { work, signIn, upload };
}

for (const draft of ['file', 'brief', 'failed']) {
  test(`L2-043.5: ${draft} upload drafts remain until Discard is chosen`, async ({ page }) => {
    const { work, upload } = await openUpload(page);
    if (draft === 'brief') await upload.fill({ intent: 'Keep this intention' });
    else await upload.chooseImage();
    if (draft === 'failed') {
      work.library.errors.upload.push('request_failed');
      await upload.save();
      await upload.expectFailure();
    }
    await upload.returnToLibrary();
    await upload.expectDiscardChoice();
    await upload.escapeDiscard();
    await upload.expectOpen();
    if (draft === 'brief') await upload.expectDraft({ intent: 'Keep this intention' });
    await upload.returnToLibrary();
    await upload.expectDiscardChoice();
    await upload.discardChanges();
    await work.expectOpen();
    await upload.open();
    await upload.expectEmptyDraft();
  });
}

test('L2-043.5: an empty upload leaves without a prompt', async ({ page }) => {
  const { work, upload } = await openUpload(page);
  await upload.returnToLibrary();
  await work.expectOpen();
  await upload.expectNoDiscardChoice();
});

test('L2-043.5: upload unload protection ends after acknowledgment', async ({ page }) => {
  const { upload } = await openUpload(page);
  await upload.expectUnloadProtection(false);
  await upload.chooseImage();
  await upload.expectUnloadProtection(true);
  await upload.save();
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning');
  await upload.expectUnloadProtection(false);
});

test('L2-043.5: keeping a pending upload waits for its saved result', async ({ page }) => {
  const { work, upload } = await openUpload(page);
  work.library.pause('upload');
  await upload.chooseImage();
  await upload.save();
  await upload.expectSaving();
  await upload.returnToLibrary();
  await upload.expectDiscardChoice();
  await upload.expectPendingWarning();
  await upload.keepEditing();
  work.library.release('upload');
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning');
  await upload.expectNoDiscardChoice();
});

test('L2-043.5: leaving an in-flight upload does not navigate back when it completes', async ({ page }) => {
  const { work, upload } = await openUpload(page);
  work.library.pause('upload');
  await upload.chooseImage();
  await upload.save();
  await expect.poll(() => work.library.calls.filter(call => call === 'upload').length).toBe(1);
  await upload.returnToLibrary();
  await upload.expectDiscardChoice();
  await upload.expectPendingWarning();
  await upload.discardChanges();
  await work.expectOpen();
  work.library.release('upload');
  await expect.poll(() => work.library.photos.length).toBe(1);
  await work.expectOpen();
});

test('L2-043.5: acknowledgment during a discard prompt completes on My Work', async ({ page }) => {
  const { work, upload } = await openUpload(page);
  work.library.pause('upload');
  await upload.chooseImage();
  await upload.save();
  await upload.expectSaving();
  await upload.returnToLibrary();
  await upload.expectDiscardChoice();
  work.library.release('upload');
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning');
  await upload.expectNoDiscardChoice();
});

test('L2-037/L2-043: discarding the modal allows sign-out and a clean upload after sign-in', async ({ page }) => {
  const { work, signIn, upload } = await openUpload(page);
  await upload.chooseImage();
  await upload.fill({ intent: 'Private unfinished intention' });
  await upload.closeDialog(); await upload.discardChanges();
  await work.signOut();
  await signIn.expectSignedOut();
  await signIn.continue();
  await upload.open();
  await upload.expectEmptyDraft();
  await upload.expectUnloadProtection(false);
});
