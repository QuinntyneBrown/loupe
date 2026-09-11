// Acceptance Test. Traces to: L2-041.2, L2-008, L2-034.
import { test, expect } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';
import { critiqueOperation } from '../fixtures/critique-operation.js';

for (const action of ['request', 'retry', 'regenerate', 'upload']) {
  test(`Azure disclosure precedes ${action} authorization`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    const id = work.library.photos[0].id;
    if (action === 'regenerate') work.library.critiques.set(id, savedCritique());
    if (action === 'retry') work.library.critiqueOperations.set(id, {
      ...critiqueOperation(id, 'Failed'), failureCode: 'provider_timeout',
      message: 'Analysis could not be completed.',
    });
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    if (action === 'upload') {
      const upload = new PhotographUploadPage(page);
      await upload.open();
      await upload.chooseImage();
      await upload.expectAzureCritiqueDisclosure();
    } else {
      await work.openPhotograph('Study 01');
      const detail = new PhotographDetailPage(page);
      if (action === 'regenerate') {
        await detail.regenerateCritique();
        await detail.expectAzureRegenerationDisclosure();
      } else {
        await detail.expectAzureCritiqueDisclosure(action === 'retry');
      }
    }
    expect(work.library.calls).not.toContain('requestCritique');
    expect(work.library.calls).not.toContain('retryCritique');
  });
}

for (const width of [375, 1440]) {
  test(`Azure upload disclosure stays visible beside actions at ${width}px`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width, height: 812 });
    const work = new MyWorkPage(page);
    await work.configureCollection(0);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    const upload = new PhotographUploadPage(page);
    await upload.open();
    await upload.chooseImage();
    await upload.expectAzureDisclosureInView();
    await upload.capture(testInfo.outputPath('azure-upload-disclosure.png'));
  });
}
