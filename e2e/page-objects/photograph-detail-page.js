import { expect } from '@playwright/test';

export class PhotographDetailPage {
  constructor(page) { this.page = page; }
  async openMissing() { await this.page.goto('/my-work/00000000-0000-4000-8000-999999999999'); }
  async expectSaved(title = 'Study 01') {
    await expect(this.page.getByRole('heading', { level: 1, name: title, exact: true })).toBeVisible();
    await expect(this.page.getByRole('img', { name: title, exact: true })).toBeVisible();
    await expect(this.page.getByText('Explore quiet morning light', { exact: true })).toBeVisible();
    await expect(this.page.getByText('Fixture camera', { exact: true })).toBeVisible();
    await expect(this.page.getByText('Keep the edges quiet. Try a lower viewpoint.', { exact: true })).toBeVisible();
  }
  async expectUnavailable() {
    await expect(this.page.getByRole('heading', { name: 'Photograph unavailable', exact: true })).toBeVisible();
    await expect(this.page.getByRole('link', { name: 'My Work', exact: true })).toBeVisible();
  }
  async expectFailure() { await expect(this.page.getByRole('alert')).toHaveText('This photograph could not be loaded. Try again.'); }
  async retry() { await this.page.getByRole('button', { name: 'Try again', exact: true }).click(); }
  async expectContentFocus() { await expect(this.page.getByRole('main')).toBeFocused(); }
  async expectAbsentContext() {
    await expect(this.page.getByText('No brief added.', { exact: true })).toBeVisible();
    await expect(this.page.getByText('No capture settings available.', { exact: true })).toBeVisible();
    await expect(this.page.getByText('No notes yet.', { exact: true })).toBeVisible();
  }
}
