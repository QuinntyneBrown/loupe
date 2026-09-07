import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

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
  async expectAccessibleLayout(sideBySide) {
    const image = await this.page.getByRole('img', { name: 'Study 01', exact: true }).boundingBox();
    const context = await this.page.getByRole('region', { name: 'Critique brief', exact: true }).boundingBox();
    if (sideBySide) {
      expect(context.x).toBeGreaterThanOrEqual(image.x + image.width);
      expect(Math.abs(context.y - image.y)).toBeLessThan(2);
    } else expect(context.y).toBeGreaterThanOrEqual(image.y + image.height);
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
  async capture(path) { await this.page.screenshot({ path }); }
}
