import { expect } from '@playwright/test';

export class ReferencePage {
  constructor(page) { this.page = page; }
  async open() { await this.page.goto('/'); }
  async expectReady() {
    await expect(this.page.getByRole('heading', { name: 'A clear frame for photography.', exact: true })).toBeVisible();
  }
  async activatePrimaryWithKeyboard() {
    await this.page.getByRole('button', { name: 'Try primary button', exact: true }).focus();
    await this.page.keyboard.press('Enter');
  }
  async expectPrimaryFeedback() {
    await expect(this.page.getByRole('status', { name: 'Button feedback' })).toHaveText('Primary button activated.');
  }
  async expectDisabledExample() {
    await expect(this.page.getByRole('button', { name: 'Unavailable example' })).toBeDisabled();
  }
  async browseTokens() {
    await this.page.getByRole('link', { name: 'Tokens', exact: true }).click();
    for (const name of ['Color', 'Spacing', 'Typography', 'Radius', 'Sizing', 'Borders', 'Elevation', 'Motion', 'Focus']) {
      const category = this.page.getByRole('region', { name, exact: true });
      await expect(category).toBeVisible();
      await expect(category.getByRole('listitem').first()).toContainText('--lp-');
    }
  }
  async expectFitsViewport() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  async expectNoAccessibilityViolations() {
    const { default: AxeBuilder } = await import('@axe-core/playwright');
    expect((await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
  }
  async expectFontsLoaded() {
    // document.fonts.check() is unreliable here — Chromium reports it true even for a
    // font name with no @font-face and no matching system font at all. document.fonts.load()
    // genuinely distinguishes "a matching @font-face exists and loaded" from "it doesn't".
    await this.page.evaluate(() => document.fonts.ready);
    for (const font of ['400 16px "Instrument Sans"', '600 16px "Instrument Sans"', '400 16px "Spline Sans Mono"']) {
      const loaded = await this.page.evaluate((f) => document.fonts.load(f).then((list) => list.length), font);
      expect(loaded, `expected a loaded @font-face for ${font}`).toBeGreaterThan(0);
    }
  }
  async openEditor() { await this.page.getByRole('button', { name: 'Try editor dialog' }).click(); }
  async expectEditorFocus() { await expect(this.page.getByRole('textbox', { name: 'Example title' })).toBeFocused(); }
  async saveEditor(title) {
    await this.page.getByRole('textbox', { name: 'Example title' }).fill(title);
    await this.page.getByRole('button', { name: 'Save example', exact: true }).click();
  }
  async expectInvalidTitle() { await expect(this.page.getByText('Enter a title for this example.', { exact: true })).toBeVisible(); }
  async expectSaved(title) {
    await expect(this.page.getByRole('dialog')).not.toBeVisible();
    await expect(this.page.getByRole('status', { name: 'Editor feedback' })).toHaveText(`Saved example: ${title}`);
    await expect(this.page.getByRole('button', { name: 'Try editor dialog' })).toBeFocused();
  }
  async cancelEditorWithKeyboard() {
    await this.page.keyboard.press('Escape');
    await expect(this.page.getByRole('dialog')).not.toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Try editor dialog' })).toBeFocused();
  }
  async expectFocusContained() {
    for (let i = 0; i < 9; i++) {
      await this.page.keyboard.press('Tab');
      expect(await this.page.getByRole('dialog').evaluate(dialog => dialog.contains(document.activeElement))).toBe(true);
    }
  }
  async browseGallery() {
    await this.page.getByRole('link', { name: 'States & layouts', exact: true }).click();
    for (const name of ['Cards and selection', 'Progress and feedback', 'Empty and error states']) {
      await expect(this.page.getByRole('heading', { name, exact: true })).toBeVisible();
    }
    await this.page.getByRole('button', { name: 'Window light', exact: true }).click();
    await expect(this.page.getByRole('button', { name: 'Window light', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(this.page.getByRole('progressbar', { name: 'Upload example' })).toHaveAttribute('value', '60');
    await expect(this.page.getByRole('progressbar', { name: 'Processing example' })).not.toHaveAttribute('value');
    await expect(this.page.getByRole('button', { name: 'Saving example' })).toBeDisabled();
  }
  async expectGridColumns(width) {
    const expected = width < 576 ? 1 : width < 768 ? 2 : width < 992 ? 3 : width < 1200 ? 4 : 5;
    const grid = this.page.getByRole('list', { name: 'Image grid example' });
    await expect(grid.getByRole('listitem')).toHaveCount(10);
    expect(await grid.evaluate(element => getComputedStyle(element).gridTemplateColumns.split(' ').length)).toBe(expected);
  }
}
