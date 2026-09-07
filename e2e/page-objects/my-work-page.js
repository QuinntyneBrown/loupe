import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { PhotographLibrary } from '../fixtures/photograph-library.js';

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
    this.library = new PhotographLibrary(count);
    this.library.failures.list = failures;
    await this.library.attach(this.page);
  }
  async failNextPage() {
    this.library.failures.list = 1;
  }
  async openPhotograph(title) { await this.page.getByRole('link', { name: title, exact: true }).click(); }
  async expectNewPhotographFocus(title) { await expect(this.page.getByRole('link', { name: title, exact: true })).toBeFocused(); }
  async openFocusedPhotograph() { await this.page.keyboard.press('Enter'); }
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
