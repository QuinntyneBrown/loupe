import { test } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// Acceptance tests: L2-001.6, L2-043.7, L2-045.2.
for (const entry of ['header', 'empty']) {
  test(`upload ${entry} action opens a modal over My Work and restores focus`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(0);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination(); await signIn.continue();
    const upload = new PhotographUploadPage(page);
    await upload.openFrom(entry);
    await upload.expectModal();
    await upload.expectFocusContained();
    await upload.closeDialog();
    await upload.expectClosedWithFocus(entry);
  });
}

test('legacy upload URL returns to My Work', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openWithDestination('/my-work/upload'); await signIn.continue();
  await work.expectOpen();
});
