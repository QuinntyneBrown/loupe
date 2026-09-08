import { expect } from '@playwright/test';
import { ReferenceLibrary } from '../fixtures/reference-library.js';

export class InspirationPage {
  constructor(page) { this.page = page; }
  async configure(count) { this.library = new ReferenceLibrary(count); await this.library.attach(this.page); }
  async open() { await this.page.getByRole('navigation', { name: 'Library', exact: true }).getByRole('link', { name: 'Inspiration', exact: true }).click(); }
  async expectTitle() { await expect(this.page).toHaveTitle('Inspiration \u00b7 Loupe'); }
  async expectEmptyFocus() { await expect(this.page.getByRole('heading', { name: 'No references yet', exact: true })).toBeFocused(); }
  async expectOpen() {
    await expect(this.page.getByRole('heading', { name: 'Inspiration', exact: true })).toBeVisible();
    await expect(this.page.getByRole('navigation', { name: 'Library', exact: true }).getByRole('link', { name: 'Inspiration', exact: true })).toHaveAttribute('aria-current', 'page');
  }
  async expectReferences(count) { await expect(this.page.getByRole('article')).toHaveCount(count); }
  async openReference(title) { await this.page.getByRole('link', { name: title, exact: true }).click(); }
  async loadMore() { await this.page.getByRole('button', { name: 'Load more references', exact: true }).click(); }
  async expectReferenceFocus(title) { await expect(this.page.getByRole('link', { name: title, exact: true })).toBeFocused(); }
  async expectEmpty() { await expect(this.page.getByRole('heading', { name: 'No references yet', exact: true })).toBeVisible(); }
  async expectFailure() { await expect(this.page.getByRole('alert')).toHaveText('Inspiration could not be loaded. Try again.'); await expect(this.page.getByRole('heading', { name: 'No references yet', exact: true })).toHaveCount(0); }
  async retry() { await this.page.getByRole('button', { name: 'Retry loading references', exact: true }).click(); }
}
