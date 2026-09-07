import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';

const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);

for (const viewport of viewports) {
  test(`L2-044/045/046: My Work grid at ${viewport.width}x${viewport.height}`, async ({ page }, info) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const work = new MyWorkPage(page);
    await work.configureCollection(25);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    const columns = viewport.width < 576 ? 1 : viewport.width < 768 ? 2 : viewport.width < 992 ? 3 : viewport.width < 1200 ? 4 : 5;
    await work.expectAccessibleGrid(columns);
    if (info.project.name === 'chromium' && [375, 1440].includes(viewport.width))
      await work.capture(info.outputPath('my-work.png'));
  });
}
