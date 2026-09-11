import { expect } from '@playwright/test';
import { ReferenceLibrary } from '../fixtures/reference-library.js';
import AxeBuilder from '@axe-core/playwright';

export class InspirationPage {
  constructor(page) { this.page = page; }
  async configure(count) { this.library = new ReferenceLibrary(count); await this.library.attach(this.page); }
  async openSave() { await this.page.getByRole('button', { name: 'Save reference', exact: true }).first().click(); }
  async expectDraftAccessible() {
    expect((await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  async importDraft(source) {
    const dialog = this.page.getByRole('dialog');
    await dialog.getByRole('radio', { name: 'From a link', exact: true }).check();
    await dialog.getByRole('textbox', { name: 'Link to the photograph', exact: true }).fill(source);
    await dialog.getByRole('button', { name: 'Import', exact: true }).click();
  }
  async expectDraftPreview() { await expect(this.page.getByRole('dialog').getByRole('textbox', { name: 'Photographer', exact: true })).toBeVisible(); }
  async expectImporting() { await expect(this.page.getByRole('dialog').getByRole('heading', { name: 'Importing…', exact: true })).toBeVisible(); }
  async expectImportFallback() { await expect(this.page.getByRole('dialog').getByRole('heading', { name: "Couldn't import this page", exact: true })).toBeVisible(); }
  async saveFallback(title, notes) { const dialog = this.page.getByRole('dialog'); await dialog.getByRole('textbox', { name: 'Title', exact: true }).fill(title); await dialog.getByRole('textbox', { name: 'Notes optional', exact: true }).fill(notes); await dialog.getByRole('button', { name: 'Save link', exact: true }).click(); }
  async expectDraftDuplicate() { await expect(this.page.getByRole('dialog').getByRole('heading', { name: 'Already saved', exact: true })).toBeVisible(); }
  async expectDraftBoardSelected(name) { await expect(this.page.getByRole('dialog').getByRole('checkbox', { name, exact: true })).toBeChecked(); }
  async newDraftBoard(name) { await this.page.getByRole('dialog').getByRole('textbox', { name: 'New board name', exact: true }).fill(name); }
  async backFromDraft() { await this.page.getByRole('dialog').getByRole('button', { name: 'Back', exact: true }).click(); await expect(this.page.getByRole('dialog').getByRole('radio', { name: 'Upload an image', exact: true })).toBeVisible(); }
  async addFallbackImage(title, notes) {
    const dialog = this.page.getByRole('dialog');
    await dialog.getByRole('textbox', { name: 'Title', exact: true }).fill(title);
    await dialog.getByRole('textbox', { name: 'Notes optional', exact: true }).fill(notes);
    await this.chooseFallbackImage();
    await this.expectDraftPreview();
  }
  async chooseFallbackImage() { await this.page.getByRole('dialog').getByLabel('Add an image', { exact: true }).setInputFiles({ name: 'Added.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1kAAAAASUVORK5CYII=', 'base64') }); }
  async expectDraftError(message) { await expect(this.page.getByRole('dialog').getByRole('alert')).toHaveText(message); }
  async expectDraftUnload(expected) { expect(await this.page.evaluate(() => !window.dispatchEvent(new Event('beforeunload', { cancelable: true })))).toBe(expected); }
  async backInBrowser() { await this.page.evaluate(() => history.back()); }
  async expectDiscardDraft() { await expect(this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true })).toBeVisible(); }
  async keepDraft() { await this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true }).getByRole('button', { name: 'Keep editing', exact: true }).click(); }
  async discardDraftNavigation() { await this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true }).getByRole('button', { name: 'Discard', exact: true }).click(); }
  async expectPendingDraftWarning() { await expect(this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true })).toContainText('This save may finish after you leave. Check Inspiration before saving again.'); }
  async startUploadDraft() {
    const dialog = this.page.getByRole('dialog');
    await dialog.getByRole('radio', { name: 'Upload an image', exact: true }).check();
    await dialog.getByLabel('Choose an image', { exact: true }).setInputFiles({ name: 'Morning.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1kAAAAASUVORK5CYII=', 'base64') });
    await dialog.getByRole('button', { name: 'Continue', exact: true }).click();
  }
  async reportDraftProgress(transferred, total) { await this.page.evaluate(detail => window.dispatchEvent(new CustomEvent('loupe-reference-draft-upload-progress', { detail })), { transferred, total }); }
  async expectDraftProgress(transferred, total) { const progress = this.page.getByRole('dialog').getByRole('progressbar', { name: 'Upload progress', exact: true }); await expect(progress).toHaveJSProperty('value', transferred); await expect(progress).toHaveJSProperty('max', total); }
  async uploadDraft() {
    const dialog = this.page.getByRole('dialog');
    await dialog.getByRole('radio', { name: 'Upload an image', exact: true }).check();
    await dialog.getByLabel('Choose an image', { exact: true }).setInputFiles({ name: 'Morning.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1kAAAAASUVORK5CYII=', 'base64') });
    await dialog.getByRole('button', { name: 'Continue', exact: true }).click();
    await expect(dialog.getByRole('heading', { name: 'Save reference', exact: true })).toBeVisible();
    await expect(dialog.getByRole('textbox', { name: 'Title', exact: true })).toBeVisible();
  }
  async editDraft(title, photographer) {
    const dialog = this.page.getByRole('dialog');
    await dialog.getByRole('textbox', { name: 'Title', exact: true }).fill(title);
    await dialog.getByRole('textbox', { name: 'Photographer', exact: true }).fill(photographer);
  }
  async saveDraft() { await this.page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click(); }
  async cancelDraft() { await this.page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await expect(this.page.getByRole('dialog')).toHaveCount(0); }
  async expectSaveClosed() { await expect(this.page.getByRole('dialog')).toHaveCount(0); await expect(this.page.getByRole('button', { name: 'Save reference', exact: true }).first()).toBeFocused(); }
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
  async filterTag(name) { await this.page.getByRole('group', { name: 'Filter by tag', exact: true }).getByRole('button', { name, exact: true }).click(); }
  async expectSelectedTag(name) { await expect(this.page.getByRole('group', { name: 'Filter by tag', exact: true }).getByRole('button', { name, exact: true })).toHaveAttribute('aria-pressed','true'); }
  async clearTags() { await this.page.getByRole('button', { name: 'Clear tags', exact: true }).click(); }
  async expectFilteredEmpty() { await expect(this.page.getByRole('heading', { name: 'Nothing matches these tags.', exact: true })).toBeVisible(); }
  async openTagDialog() { await this.page.getByRole('button', { name: 'Filter by tag', exact: true }).click(); }
  async selectDialogTag(name) { await this.page.getByRole('dialog', { name: 'Filter by tag', exact: true }).getByRole('button', { name, exact: true }).click(); }
  async applyTagDialog(count) { await this.page.getByRole('dialog').getByRole('button', { name: `Show ${count} references`, exact: true }).click(); }
  async closeTagDialog() { await this.page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); }
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
  async expectEmptyFocus() { await expect(this.page.getByRole('heading', { name: 'Save the photographs that move you.', exact: true })).toBeFocused(); }
  async expectOpen() {
    await expect(this.page.getByRole('heading', { name: 'Inspiration', exact: true })).toBeVisible();
    await expect(this.page.getByRole('navigation', { name: 'Library', exact: true }).getByRole('link', { name: 'Inspiration', exact: true })).toHaveAttribute('aria-current', 'page');
  }
  async expectReferences(count) { await expect(this.page.getByRole('article')).toHaveCount(count); }
  async openReference(title) { await this.page.getByRole('link', { name: title, exact: true }).click(); }
  async loadMore() { await this.page.getByRole('button', { name: 'Load more references', exact: true }).click(); }
  async expectReferenceFocus(title) { await expect(this.page.getByRole('link', { name: title, exact: true })).toBeFocused(); }
  async expectEmpty() { await expect(this.page.getByRole('heading', { name: 'Save the photographs that move you.', exact: true })).toBeVisible(); }
  async expectLoadingSkeleton(count) { await expect(this.page.locator('.lp-skeleton--tile')).toHaveCount(count); }
  async expectFailure() { const alert = this.page.getByRole('region', { name: 'Your references', exact: true }).getByRole('alert'); await expect(alert).toContainText("Couldn't load your references."); await expect(alert).toContainText('Check your connection and try again. Nothing you saved is lost.'); await expect(this.page.getByRole('heading', { name: 'Save the photographs that move you.', exact: true })).toHaveCount(0); }
  async retry() { await this.page.getByRole('region', { name: 'Your references', exact: true }).getByRole('button', { name: 'Try again', exact: true }).click(); }
  async saveFromEmpty() { await this.page.getByRole('region', { name: 'Your references', exact: true }).getByRole('button', { name: 'Save reference', exact: true }).click(); }
  async expectEmptySaveFocus() { await expect(this.page.getByRole('region', { name: 'Your references', exact: true }).getByRole('button', { name: 'Save reference', exact: true })).toBeFocused(); }
}
