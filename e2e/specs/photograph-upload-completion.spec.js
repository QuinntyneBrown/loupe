import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// Acceptance: L2-001.8, L2-043.7. Completion preserves My Work and reconciles its collection.
for (const critique of [false, true]) {
  test(`completion stays on My Work with a View notification, critique=${critique}`, async ({ page }) => {
    const work = new MyWorkPage(page); await work.configureCollection(25);
    const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
    await work.loadMore(); await work.expectPhotographs(25);
    const upload = new PhotographUploadPage(page); await upload.open(); await upload.chooseImage();
    if (critique) await upload.requestCritiqueAfterSaving();
    await upload.save(); await upload.expectCompleted(critique);
    await work.expectPhotographs(24); await work.openPhotograph('Morning');
    expect(work.library.photos).toHaveLength(26);
  });
}

test('failed completion refresh retains cards and retries without losing the saved View link', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(1);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await work.expectPhotographs(1);
  const upload = new PhotographUploadPage(page); await upload.open(); await upload.chooseImage();
  work.library.failures.list = 1;
  await upload.save(); await upload.expectCompleted(false);
  await work.expectFailure(); await work.expectPhotographs(1);
  await work.retry(); await work.expectPhotographs(2);
  await upload.viewCompletedPhotograph();
});
