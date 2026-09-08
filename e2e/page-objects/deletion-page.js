import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export class DeletionPage {
  constructor(page) { this.page = page; }
  async expectPending() {
    await expect(this.page).toHaveURL(/\/deletions\/[a-f0-9-]+$/);
    await expect(this.page.getByRole('heading', { name: 'Deletion status', exact: true })).toBeVisible();
    await expect(this.page.getByRole('status')).toHaveText('This item is no longer in your library. Its files are queued for removal.');
    await expect(this.page.getByRole('main')).toBeFocused();
  }
  async expectCompleted() {
    await expect(this.page.getByRole('status')).toHaveText('Cleanup completed. The item’s active files have been removed.', { timeout: 10000 });
  }
  async backToMyWork() { await this.page.getByRole('main').getByRole('link', { name: 'My Work', exact: true }).click(); }
  async expectFailure() { await expect(this.page.getByRole('alert')).toHaveText('Cleanup status could not be loaded. Try again.'); }
  async checkStatus() { await this.page.getByRole('button', { name: 'Check cleanup status', exact: true }).click(); }
  async expectUnavailable() { await expect(this.page.getByRole('heading', { name: 'Deletion record unavailable', exact: true })).toBeVisible(); }
  async expectPendingMessage() { await expect(this.page.getByRole('status')).toHaveText('This item is no longer in your library. Its files are queued for removal.'); }
  async expectAccessibleStatus() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const button = await this.page.getByRole('button', { name: 'Check cleanup status', exact: true }).boundingBox();
    expect(button.width).toBeGreaterThanOrEqual(24);
    expect(button.height).toBeGreaterThanOrEqual(24);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
}
