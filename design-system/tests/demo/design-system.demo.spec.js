import { test, expect } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { ReferencePage } from '../page-objects/reference-page.js';
import { showTitleCard, hideTitleCard, showCaption, hideCaption } from './narration.mjs';

async function slowScrollThrough(page, ms) {
  const steps = 10;
  for (let i = 0; i < steps; i++) {
    await page.mouse.wheel(0, 140);
    await page.waitForTimeout(ms / steps);
  }
}

test('design-system demo: the authoritative visual reference', async ({ page }, testInfo) => {
  test.setTimeout(6 * 60_000);
  const reference = new ReferencePage(page);

  await page.goto('/');
  await showTitleCard(page, 'Loupe Design System', 'The authoritative visual reference — tokens and component states, live');
  await page.waitForTimeout(4500);
  await hideTitleCard(page);

  await showCaption(page, 'A keyboard-operable primitive, not a picture of one');
  await reference.expectReady();
  await page.waitForTimeout(2200);
  await reference.activatePrimaryWithKeyboard();
  await reference.expectPrimaryFeedback();
  await page.waitForTimeout(3200);
  const disabled = page.getByRole('button', { name: 'Unavailable example' });
  await disabled.scrollIntoViewIfNeeded();
  await reference.expectDisabledExample();
  await page.waitForTimeout(2000);

  await showCaption(page, 'Every interactive example is real', 'Dialog focus trapping, validation, and save feedback — not a static mockup');
  await reference.openEditor();
  await reference.expectEditorFocus();
  await page.waitForTimeout(1600);
  await page.getByRole('button', { name: 'Save example', exact: true }).click();
  await reference.expectInvalidTitle();
  await page.waitForTimeout(2200);
  await reference.saveEditor('Golden hour rooftop series');
  await reference.expectSaved('Golden hour rooftop series');
  await page.waitForTimeout(2800);

  await showCaption(page, 'Design tokens are the single source of truth', 'Color, spacing, type, radius, sizing, borders, elevation, motion, focus');
  await page.getByRole('link', { name: 'Tokens', exact: true }).click();
  for (const name of ['Color', 'Spacing', 'Typography', 'Radius', 'Sizing', 'Borders', 'Elevation', 'Motion', 'Focus']) {
    const category = page.getByRole('region', { name, exact: true });
    await category.scrollIntoViewIfNeeded();
    await expect(category).toBeVisible();
    await expect(category.getByRole('listitem').first()).toContainText('--lp-');
    await page.waitForTimeout(1500);
  }

  await showCaption(page, 'Component states and responsive layouts', 'Cards, selection, progress, empty and error states, the image grid');
  await page.getByRole('link', { name: 'States & layouts', exact: true }).click();
  for (const name of ['Cards and selection', 'Progress and feedback', 'Empty and error states']) {
    const heading = page.getByRole('heading', { name, exact: true });
    await heading.scrollIntoViewIfNeeded();
    await expect(heading).toBeVisible();
    await page.waitForTimeout(1800);
  }
  await page.getByRole('button', { name: 'Window light', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Window light', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.waitForTimeout(1800);
  const grid = page.getByRole('list', { name: 'Image grid example' });
  await grid.scrollIntoViewIfNeeded();
  await expect(grid.getByRole('listitem')).toHaveCount(10);
  await slowScrollThrough(page, 3000);

  await hideCaption(page);
  await showTitleCard(page, 'No application data. No runtime API calls.', 'A static site the application mirrors — see design-system/README.md');
  await page.waitForTimeout(4000);

  const video = page.video();
  await page.close();
  if (video) {
    mkdirSync(testInfo.outputDir, { recursive: true });
    await video.saveAs(`${testInfo.outputDir}/design-system.webm`);
  }
});
