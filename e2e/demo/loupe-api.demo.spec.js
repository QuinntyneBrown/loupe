import { test, expect } from '@playwright/test';
import { mkdirSync } from 'node:fs';

test('loupe-api demo: real Loupe.Api and Loupe.Worker', async ({ page }, testInfo) => {
  test.setTimeout(5 * 60_000);
  await page.goto('/');
  await expect(page.locator('#caption')).toBeVisible();

  // The page drives itself (see loupe-api-terminal.html); wait for it to report done.
  await expect(page.locator('#status')).toHaveAttribute('data-demo-done', 'true', { timeout: 4 * 60_000 });
  const ok = await page.locator('#status').getAttribute('data-demo-ok');
  expect(ok, 'every recorded step must have succeeded against the real backend').toBe('true');

  await page.waitForTimeout(2500); // let the closing state sit on screen before the video ends

  const video = page.video();
  await page.close();
  if (video) {
    mkdirSync(testInfo.outputDir, { recursive: true });
    await video.saveAs(`${testInfo.outputDir}/loupe-api.webm`);
  }
});
