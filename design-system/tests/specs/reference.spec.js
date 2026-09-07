// Acceptance Test
// Traces to: L2-051, L2-045
// Description: Independently served reference with a keyboard-operable primitive.
import { test } from '@playwright/test';
import { ReferencePage } from '../page-objects/reference-page.js';

test('L2-051.1/.3 and L2-045.1: use the standalone button example with a keyboard', async ({ page }) => {
  const reference = new ReferencePage(page);
  await reference.open();
  await reference.expectReady();
  await reference.activatePrimaryWithKeyboard();
  await reference.expectPrimaryFeedback();
  await reference.expectDisabledExample();
});
