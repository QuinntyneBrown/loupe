import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);

for (const viewport of viewports) {
  test(`L2-044/045/046: photograph detail at ${viewport.width}x${viewport.height}`, async ({ page }, info) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    await work.openPhotograph('Study 01');
    const detail = new PhotographDetailPage(page);
    await detail.expectSaved();
    await detail.expectAccessibleLayout(viewport.width >= 992);
    if (info.project.name === 'chromium' && [375, 1440].includes(viewport.width))
      await detail.capture(info.outputPath('detail.png'));
  });
}
