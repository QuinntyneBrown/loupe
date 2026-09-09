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

for (const dismissal of ['Escape', 'backdrop', 'browser Back']) {
  test(`dirty dialog protects its draft on ${dismissal}`, async ({ page }) => {
    const work = new MyWorkPage(page); await work.configureCollection(0);
    const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
    if (dismissal === 'browser Back') await work.visitInspirationThenReturn();
    const upload = new PhotographUploadPage(page); await upload.open();
    await upload.fill({ intent: 'Retain my context' });
    const dismiss = () => dismissal === 'Escape' ? upload.escapeDialog() : dismissal === 'backdrop' ? upload.clickBackdrop() : upload.back();
    await dismiss(); await upload.expectDiscardChoice(); await upload.keepEditing();
    await upload.expectModal();
    await upload.expectDraft({ intent: 'Retain my context' });
    await dismiss(); await upload.expectDiscardChoice(); await upload.discardChanges();
    if (dismissal === 'browser Back') await work.expectInspiration(); else await work.expectOpen();
  });
}
