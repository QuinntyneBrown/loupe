import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';

test('fonts: sign-in loads self-hosted Instrument Sans and Spline Sans Mono', async ({ page }) => {
  const signIn = new SignInPage(page);
  await signIn.openFailedCallback();
  await signIn.expectFontsLoaded();
});

const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);

for (const viewport of viewports) {
  test(`L2-044/045/046: sign-in reflows and remains accessible at ${viewport.width}x${viewport.height}`, async ({ page }, info) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const signIn = new SignInPage(page);
    await signIn.openFailedCallback();
    await signIn.expectRetryableFailure();
    await signIn.expectAccessibleLayout();
    if (info.project.name === 'chromium' && [375, 1440].includes(viewport.width))
      await signIn.capture(info.outputPath('sign-in.png'));
  });
}
