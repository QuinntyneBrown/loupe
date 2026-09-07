import { expect } from '@playwright/test';

export class MyWorkPage {
  constructor(page) { this.page = page; }
  async expectOpen() {
    await expect(this.page).toHaveURL(/\/my-work$/);
    await expect(this.page.getByRole('heading', { name: 'My Work', exact: true })).toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Sign out', exact: true })).toBeVisible();
  }
  async signOut() { await this.page.getByRole('button', { name: 'Sign out', exact: true }).click(); }
}
