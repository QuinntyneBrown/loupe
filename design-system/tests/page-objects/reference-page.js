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
    await expect(this.page.getByRole('status')).toHaveText('Primary button activated.');
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
}
