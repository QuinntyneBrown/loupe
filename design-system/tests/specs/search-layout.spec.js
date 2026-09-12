import { test } from '@playwright/test';
import { SearchPage } from '../page-objects/search-page.js';

test.describe.configure({ timeout: 60_000 });

test('given the reference-first mixed example on narrow screens, the first row is filled before a wide photographer card', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 900 });
  const search = new SearchPage(page);
  await search.open();
  await search.chooseState('Results');
  await search.expectStatus('4 of 6 results');
  await search.expectReferenceFirstRow();
});

for (const width of [320, 375, 639, 640, 641, 768, 1023, 1024, 1025, 1440]) {
  test(`given mixed cards at ${width}, reference tiles and two-column photographer cards reflow without overflow`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    const search = new SearchPage(page);
    await search.open();
    await search.chooseState('Results');
    await search.expectStatus('4 of 6 results');
    await search.expectMixedLayout(width);
    await search.expectFitsViewport();
    await search.expectTargetsUsable();
    if (width === 320 || width === 1440) await search.screenshot(testInfo.outputPath(`mixed-${width}.png`));
    await search.search('architecture');
    await search.expectStatus('2 of 2 results');
    await search.expectFitsViewport();
    await search.expectSafeSources();
    if (width === 320) await search.screenshot(testInfo.outputPath('long-content-320.png'));
  });
}

for (const [width, height] of [[320, 667], [375, 667], [844, 390]]) {
  test(`given the filter dialog at ${width}x${height}, keyboard selection and all closing actions stay reachable`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width, height });
    const search = new SearchPage(page);
    await search.open();
    await search.openFilters();
    await search.expectDialogUsable();
    await search.expectKeyboardDialog();
    await search.expectNoAccessibilityViolations();
    await search.screenshot(testInfo.outputPath(`dialog-${width}x${height}.png`), false);
    await search.escapeFilters();
    await search.expectFilterReturnFocus();
    await search.expectFitsViewport();
  });
}

for (const state of ['Initial', 'Results', 'Loading', 'Empty', 'Failure', 'Unavailable filters']) {
  test(`given the ${state.toLowerCase()} example, its rendered controls and announcements pass Chromium Axe`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width: 375, height: 667 });
    const search = new SearchPage(page);
    await search.open();
    if (state !== 'Initial') await search.chooseState(state);
    if (state === 'Results') await search.expectStatus('4 of 6 results');
    if (state === 'Loading') await search.expectLoading();
    if (state === 'Empty') await search.expectEmpty('neon rain');
    if (state === 'Failure') await search.expectFailure();
    if (state === 'Unavailable filters') await search.expectUnavailableBoard();
    await search.expectFitsViewport();
    await search.expectNoAccessibilityViolations();
    await search.expectOnlyLocalRequests();
    await search.screenshot(testInfo.outputPath(`${state.toLowerCase().replaceAll(' ', '-')}-375.png`));
  });
}

test('given narrow reflow with increased text spacing and reduced motion, search and dialog controls remain usable', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 667 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const search = new SearchPage(page);
  await search.open();
  await search.useTextSpacing();
  await search.chooseState('Loading');
  await search.expectLoading();
  await search.expectReducedMotion();
  await search.completeLoading();
  await search.expectStatus('4 of 6 results');
  await search.expectFitsViewport();
  await search.openFilters();
  await search.expectDialogUsable();
  await search.toggleFilter('Tags', 'architecturalgeometryanduninterruptedshadowstudies');
  await search.applyFilters();
  await search.expectFitsViewport();
  await search.expectQuery('window');
});
