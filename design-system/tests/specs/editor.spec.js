// Acceptance Test
// Traces to: L2-051, L2-045, L2-044
// Description: Operate form and modal specimens, including errors and keyboard recovery.
import { test } from '@playwright/test';
import { ReferencePage } from '../page-objects/reference-page.js';

// Includes a full axe scan and two dialog visits; interaction assertions retain
// their 5-second deadlines. Product latency has separate L2-047 release budgets.
test.describe.configure({ timeout: 60_000 });

for (const [width, height] of [[375, 667], [768, 900], [1440, 900], [844, 390]]) {
  test(`L2-051.3/.4 and L2-045.2/.3: edit a synthetic example at ${width}x${height}`, async ({ page }) => {
    await page.setViewportSize({ width, height });
    const reference = new ReferencePage(page);
    await reference.open();
    await reference.openEditor();
    await reference.expectEditorFocus();
    await reference.expectFocusContained();
    await reference.saveEditor('');
    await reference.expectInvalidTitle();
    await reference.expectNoAccessibilityViolations();
    await reference.saveEditor('Window light study');
    await reference.expectSaved('Window light study');
    await reference.openEditor();
    await reference.cancelEditorWithKeyboard();
    await reference.expectFitsViewport();
  });
}
