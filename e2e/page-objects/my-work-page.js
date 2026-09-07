import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export class MyWorkPage {
  constructor(page) { this.page = page; }
  async expectOpen() {
    await expect(this.page).toHaveURL(/\/my-work$/);
    await expect(this.page.getByRole('heading', { name: 'My Work', exact: true })).toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Sign out', exact: true })).toBeVisible();
  }
  async signOut() { await this.page.getByRole('button', { name: 'Sign out', exact: true }).click(); }
  async expectContentFocus() { await expect(this.page.getByRole('main')).toBeFocused(); }
  async configureCollection(count, failures = 0) {
    await this.page.addInitScript(({ count, failures }) => {
      window.loupeFixture = { photoCount: count, photoListFailures: failures };
    }, { count, failures });
  }
  async failNextPage() { await this.page.evaluate(() => { window.loupeFixture.photoListFailures = 1; }); }
  async expectPhotographs(count) {
    await expect(this.page.getByRole('article')).toHaveCount(count);
    if (count) await expect(this.page.getByRole('heading', { name: 'Study 01', exact: true })).toBeVisible();
  }
  async loadMore() { await this.page.getByRole('button', { name: 'Load more photographs', exact: true }).click(); }
  async expectEnd() { await expect(this.page.getByRole('button', { name: 'Load more photographs', exact: true })).toHaveCount(0); }
  async expectEmpty() { await expect(this.page.getByRole('heading', { name: 'No photographs yet', exact: true })).toBeVisible(); }
  async expectFailure() {
    await expect(this.page.getByRole('alert')).toHaveText('My Work could not be loaded. Try again.');
    await expect(this.page.getByRole('heading', { name: 'No photographs yet', exact: true })).toHaveCount(0);
  }
  async retry() { await this.page.getByRole('button', { name: 'Try again', exact: true }).click(); }
  async expectAccessibleGrid(columns) {
    const cards = this.page.getByRole('article');
    await expect(cards).toHaveCount(24);
    const firstTop = (await cards.first().boundingBox()).y;
    let firstRow = 0;
    for (let index = 0; index < 6; index++) {
      if (Math.abs((await cards.nth(index).boundingBox()).y - firstTop) < 2) firstRow++;
    }
    expect(firstRow).toBe(columns);
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
  async capture(path) { await this.page.screenshot({ path, fullPage: false }); }
}
