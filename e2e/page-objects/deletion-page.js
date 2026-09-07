import { expect } from '@playwright/test';

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
  async backToMyWork() { await this.page.getByRole('link', { name: 'My Work', exact: true }).click(); }
}
