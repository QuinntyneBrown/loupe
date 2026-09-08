import { test } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

for (const width of [320, 1440]) {
  test(`L2-008.2/L2-044/045: saved upload recovery handles a long title at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const work = new MyWorkPage(page); await work.configureCollection(0);
    work.library.errors.requestCritique = ['integration_not_configured'];
    const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
    const upload = new PhotographUploadPage(page); await upload.open(); await upload.chooseImage();
    await upload.fill({ title: 'LongTitle'.repeat(22) });
    await upload.requestCritiqueAfterSaving(); await upload.save();
    await upload.expectSavedBeforeCritique(); await upload.expectCritiqueNotQueued();
    await upload.expectAccessibleSavedPhotograph();
  });
}
