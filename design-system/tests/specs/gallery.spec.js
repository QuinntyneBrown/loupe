// Acceptance Test
// Traces to: L2-051, L2-044, L2-045, L2-046
// Description: Explore synthetic component states and the specified image-grid breakpoints.
import { test } from '@playwright/test';
import { ReferencePage } from '../page-objects/reference-page.js';

test('L2-051.3: explore selected, loading, empty and failed component examples', async ({ page }) => {
  const reference = new ReferencePage(page);
  await reference.open();
  await reference.browseGallery();
  await reference.expectNoAccessibilityViolations();
});
for (const width of [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]) {
  test(`L2-051.4 and L2-044.1: demonstrate image grid at ${width}`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const reference = new ReferencePage(page);
    await reference.open();
    await reference.expectGridColumns(width);
    await reference.expectFitsViewport();
  });
}
