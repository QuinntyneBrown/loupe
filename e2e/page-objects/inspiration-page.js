import { expect } from '@playwright/test';
import { ReferenceLibrary } from '../fixtures/reference-library.js';

export class InspirationPage {
  constructor(page) { this.page = page; }
  async configure(count) { this.library = new ReferenceLibrary(count); await this.library.attach(this.page); }
  async expectBoard(name, count) {
    const board = this.page.getByRole('navigation', { name: 'Boards', exact: true }).getByRole('link', { name, exact: true });
    await expect(board).toBeVisible();
    await expect(board).toContainText(String(count));
  }
  async selectBoard(name) { await this.page.getByRole('navigation', { name: 'Boards', exact: true }).getByRole('link', { name, exact: true }).click(); }
  async expectBoardTitle(name) { await expect(this.page.getByRole('heading', { name, exact: true, level: 1 })).toBeVisible(); }
  async newBoard(name) {
    await this.page.getByRole('button', { name: 'New board', exact: true }).click();
    const dialog = this.page.getByRole('dialog', { name: 'New board', exact: true });
    await dialog.getByRole('textbox', { name: 'Name', exact: true }).fill(name);
    await dialog.getByRole('button', { name: 'Create board', exact: true }).click();
  }
  async renameBoard(name) {
    await this.page.getByRole('button', { name: 'Board actions', exact: true }).click();
    await this.page.getByRole('button', { name: 'Rename board', exact: true }).click();
    const dialog = this.page.getByRole('dialog', { name: 'Rename board', exact: true });
    await dialog.getByRole('textbox', { name: 'Name', exact: true }).fill(name);
    await dialog.getByRole('button', { name: 'Save', exact: true }).click();
  }
  async deleteBoard(name, confirm) {
    await this.page.getByRole('button', { name: 'Board actions', exact: true }).click();
    await this.page.getByRole('button', { name: 'Delete board', exact: true }).click();
    const dialog = this.page.getByRole('dialog', { name: `Delete “${name}”?`, exact: true });
    await expect(dialog).toContainText('references');
    await dialog.getByRole('button', { name: confirm ? 'Delete board' : 'Cancel', exact: true }).click();
  }
  async assignBoards(title, names) {
    await this.openBoardPicker(title);
    const dialog = this.page.getByRole('dialog', { name: 'Add to boards', exact: true });
    for (const name of names) await dialog.getByRole('checkbox', { name, exact: true }).check();
    await this.saveBoardPicker();
  }
  async openBoardPicker(title) {
    await this.card(title).getByRole('link', { name: title, exact: true }).focus();
    await this.card(title).getByRole('button', { name: 'Add to boards', exact: true }).click();
  }
  async newPickerBoard(name) { await this.page.getByRole('dialog').getByRole('textbox', { name: 'New board name', exact: true }).fill(name); }
  async saveBoardPicker() { await this.page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click(); }
  async expectPickerSelection(name) { await expect(this.page.getByRole('dialog').getByRole('checkbox', { name, exact: true })).toBeChecked(); }
  async removeFromBoard(title) {
    await this.card(title).getByRole('link', { name: title, exact: true }).focus();
    await this.card(title).getByRole('button', { name: 'Remove from this board', exact: true }).click();
  }
  async undoRemoval() { await this.page.getByRole('button', { name: 'Undo', exact: true }).click(); }
  async expectBoardActionFocus(title) { await expect(this.card(title).getByRole('button', { name: 'Add to boards', exact: true })).toBeFocused(); }
  async expectBoardError(message) { await expect(this.page.getByRole('dialog').getByRole('alert')).toHaveText(message); }
  async cancelBoardDialog() { await this.page.getByRole('dialog').getByRole('button', { name: 'Cancel', exact: true }).click(); }
  card(title) { return this.page.getByRole('article').filter({ has: this.page.getByRole('link', { name: title, exact: true }) }); }
  async expectSourceOverlay(title, attribution, source) {
    const card = this.card(title);
    await card.getByRole('link', { name: title, exact: true }).focus();
    await expect(card.getByText(attribution, { exact: true })).toBeVisible();
    const link = card.getByRole('link', { name: 'Open source page', exact: true });
    await expect(link).toHaveAttribute('href', source);
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    await expect(link).toHaveAttribute('referrerpolicy', 'no-referrer');
    await expect(card.getByText('Saved reference', { exact: true })).toHaveCount(0);
  }
  async expectUnknownSource(title) {
    const card = this.card(title);
    await card.getByRole('link', { name: title, exact: true }).focus();
    await expect(card.getByText('Unknown photographer', { exact: true })).toBeVisible();
    await expect(card.getByRole('link', { name: 'Open source page', exact: true })).toHaveCount(0);
    await expect(card.getByRole('img', { name: 'No preview available', exact: true })).toBeVisible();
  }
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
  async expectLoadingSkeleton(count) { await expect(this.page.locator('.lp-skeleton--tile')).toHaveCount(count); }
  async expectFailure() { await expect(this.page.getByRole('alert')).toHaveText('Inspiration could not be loaded. Try again.'); await expect(this.page.getByRole('heading', { name: 'No references yet', exact: true })).toHaveCount(0); }
  async retry() { await this.page.getByRole('button', { name: 'Retry loading references', exact: true }).click(); }
}
