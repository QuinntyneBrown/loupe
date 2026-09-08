// Acceptance Test
// Traces to: L2-051, L2-044, L2-045, L2-046
// Description: Browse the independently rendered token reference at required viewports.
import { test, expect } from '@playwright/test';
import { ReferencePage } from '../page-objects/reference-page.js';

test('L2-051.2/.5: browse every token category without external requests or runtime errors', async ({ page }) => {
  const external = [], errors = [];
  page.on('request', request => { if (new URL(request.url()).origin !== 'http://127.0.0.1:4187') external.push(request.url()); });
  page.on('pageerror', error => errors.push(error.message));
  const reference = new ReferencePage(page);
  await reference.open();
  await reference.browseTokens();
  await reference.expectNoAccessibilityViolations();
  await reference.expectFontsLoaded();
  expect(external).toEqual([]);
  expect(errors).toEqual([]);
});

for (const [width, height] of [...[320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920].map(width => [width, 900]), [375, 667], [844, 390]]) {
  test(`L2-051.4: token reference reflows at ${width}x${height}`, async ({ page }) => {
    await page.setViewportSize({ width, height });
    const reference = new ReferencePage(page);
    await reference.open();
    await reference.browseTokens();
    await reference.expectFitsViewport();
  });
}
