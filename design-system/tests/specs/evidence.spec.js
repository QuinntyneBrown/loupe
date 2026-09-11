import { test } from '@playwright/test';
import { ReferencePage } from '../page-objects/reference-page.js';

for (const width of [375, 1440]) {
  test(`L2-051/L2-054: evidence specimen supports pointer and keyboard at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const reference = new ReferencePage(page);
    await reference.open();
    // Given the standalone evidence specimen, its image area begins unselected.
    await reference.expectExampleEvidenceVisible(false);
    // Hover reveals the image area, and leaving the example hides it.
    await reference.hoverExampleEvidence();
    await reference.expectExampleEvidenceVisible(true);
    await reference.leaveExampleEvidence();
    await reference.expectExampleEvidenceVisible(false);
    // Keyboard users receive the same preview while the evidence button has focus.
    await reference.focusExampleEvidenceWithKeyboard();
    await reference.expectExampleEvidenceVisible(true);
    await reference.blurExampleEvidence();
    await reference.expectExampleEvidenceVisible(false);
    await reference.expectFitsViewport();
  });
}
