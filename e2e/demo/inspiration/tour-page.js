import { expect } from '@playwright/test';
import { InspirationPage } from '../../page-objects/inspiration-page.js';
import { SearchPage } from '../../page-objects/search-page.js';
import { SignInPage } from '../../page-objects/sign-in-page.js';

// Demo-only page objects: the same DOM knowledge as the acceptance page objects,
// plus visible pacing (typed input, image decoding) so the take reads well.
const typing = { delay: 55 };

export class SignInTourPage extends SignInPage {
  async typeCredentials(email, password) {
    await this.page.getByLabel('Email', { exact: true }).pressSequentially(email, typing);
    await this.page.getByLabel('Password', { exact: true }).pressSequentially(password, typing);
  }
  async submit() { await this.page.getByRole('button', { name: 'Sign in', exact: true }).click(); }
}

export class InspirationTourPage extends InspirationPage {
  async ready() {
    await this.expectOpen();
    await expect(this.page.getByRole('region', { name: 'Your references', exact: true })).toHaveAttribute('aria-busy', 'false');
    await this.decodeVisibleImages();
    await this.page.evaluate(() => document.fonts.ready);
  }
  async decodeVisibleImages() {
    await this.page.locator('article img').evaluateAll(images => Promise.all(images.filter(i => { const r = i.getBoundingClientRect(); return r.top < innerHeight && r.bottom > 0; }).map(i => i.decode())));
    const loaded = await this.page.locator('article img').evaluateAll(images => images.filter(i => { const r = i.getBoundingClientRect(); return r.top < innerHeight && r.bottom > 0; }).every(i => i.complete && i.naturalWidth > 0));
    expect(loaded, 'every visible reference image decoded with real pixels').toBe(true);
  }
  async expectUnselectedTag(name) { await expect(this.page.getByRole('group', { name: 'Filter by tag', exact: true }).getByRole('button', { name, exact: true })).toHaveAttribute('aria-pressed', 'false'); }
  async expectSummary(text) { await expect(this.page.locator('.lp-page-header__sub')).toHaveText(text); }
  async hoverCard(title) { await this.card(title).hover(); }
  async expectOverlay(title, attribution, source) {
    const card = this.card(title);
    await expect(card.getByText(attribution, { exact: true })).toBeVisible();
    await expect(card.getByRole('link', { name: 'Open source page', exact: true })).toHaveAttribute('href', source);
  }
  async expectFirstCard(title) { await expect(this.page.getByRole('article').first().getByRole('link', { name: title, exact: true })).toHaveCount(1); }
  async allReferences() { await this.page.getByRole('navigation', { name: 'Boards', exact: true }).getByRole('link', { name: 'All references', exact: true }).click(); }
  // Focusing <main> after navigation scrolls the heading under the sticky header; scroll back up as a viewer would.
  async top() { await this.page.evaluate(() => window.scrollTo({ top: 0, behavior: 'smooth' })); await expect.poll(() => this.page.evaluate(() => window.scrollY)).toBe(0); }
  dialog() { return this.page.getByRole('dialog'); }
  async chooseUpload() { await this.dialog().getByRole('radio', { name: 'Upload an image', exact: true }).check(); }
  async chooseFile(path) { await this.dialog().getByLabel('Choose an image', { exact: true }).setInputFiles(path); }
  async continueDraft() { await this.dialog().getByRole('button', { name: 'Continue', exact: true }).click(); }
  async expectPreview() {
    const preview = this.dialog().getByRole('img', { name: 'Preview of the reference image', exact: true });
    await expect(preview).toBeVisible();
    await preview.evaluate(i => i.decode());
    await expect.poll(() => preview.evaluate(i => i.complete && i.naturalWidth > 0), { message: 'the draft preview is a decoded image' }).toBe(true);
    await expect(this.dialog().getByRole('textbox', { name: 'Photographer', exact: true })).toBeVisible();
  }
  async typeDraft(title, photographer) {
    const titleField = this.dialog().getByRole('textbox', { name: 'Title', exact: true });
    await titleField.fill(''); await titleField.pressSequentially(title, typing);
    await this.dialog().getByRole('textbox', { name: 'Photographer', exact: true }).pressSequentially(photographer, typing);
  }
  async tickDraftBoard(name) { await this.dialog().getByRole('checkbox', { name, exact: true }).check(); }
  async typeNewBoard(name) {
    await this.page.getByRole('button', { name: 'New board', exact: true }).click();
    const dialog = this.page.getByRole('dialog', { name: 'New board', exact: true });
    await dialog.getByRole('textbox', { name: 'Name', exact: true }).pressSequentially(name, typing);
    await dialog.getByRole('button', { name: 'Create board', exact: true }).click();
  }
  async pickBoard(title, name) {
    await this.openBoardPicker(title);
    const dialog = this.page.getByRole('dialog', { name: 'Add to boards', exact: true });
    await expect(dialog).toBeVisible();
    await dialog.getByRole('checkbox', { name, exact: true }).check();
    await this.saveBoardPicker();
    await expect(dialog).toHaveCount(0);
  }
}

export class ReferenceTourPage {
  constructor(page) { this.page = page; }
  async expectOpen(title) { await expect(this.page.getByRole('heading', { name: title, exact: true, level: 1 })).toBeVisible(); await this.expectImage(title); }
  async expectImage(title) {
    const image = this.page.getByRole('main').getByRole('img', { name: title, exact: true });
    await expect(image).toBeVisible();
    await image.evaluate(i => i.decode());
    expect(await image.evaluate(i => i.complete && i.naturalWidth > 0), 'the reference image decoded with real pixels').toBe(true);
  }
  tags() { return this.page.getByRole('region', { name: 'Your tags', exact: true }); }
  notes() { return this.page.getByRole('region', { name: 'Your notes', exact: true }); }
  async revealTags() { await this.tags().scrollIntoViewIfNeeded(); }
  async typeTag(name) { const input = this.tags().getByRole('textbox', { name: 'Add a tag', exact: true }); await input.pressSequentially(name, typing); await input.press('Enter'); }
  async expectTag(name) { await expect(this.tags().getByRole('button', { name: `Remove tag ${name}`, exact: true })).toBeVisible(); }
  async expectTagsSaved() { await expect(this.tags().getByText('Saved', { exact: true })).toBeVisible(); }
  async revealNotes() { await this.notes().scrollIntoViewIfNeeded(); }
  async appendNote(text) { const field = this.notes().getByRole('textbox'); await field.focus(); await field.press('End'); await field.pressSequentially(text, typing); }
  async saveNotes() { await this.notes().getByRole('button', { name: 'Save', exact: true }).click(); }
  async expectNotesSaved() { await expect(this.notes().getByText('Saved', { exact: true })).toBeVisible(); await expect(this.notes().getByRole('button', { name: 'Save', exact: true })).toBeDisabled(); }
  async expectBoard(name) { await expect(this.page.getByRole('region', { name: 'Boards', exact: true }).getByRole('link', { name, exact: true })).toBeVisible(); }
}

export class SearchTourPage extends SearchPage {
  async typeQuery(value) {
    const field = this.page.getByRole('searchbox', { name: 'Search your library' });
    await field.fill(''); await field.pressSequentially(value, typing); await field.press('Enter');
  }
  async expectPhotographerCard(name, count) {
    const card = this.card(name);
    await expect(card).toBeVisible();
    await expect(card).toContainText(`${count} reference${count === 1 ? '' : 's'}`);
  }
  async decodeResultImages() {
    const images = this.results().locator('img');
    await images.evaluateAll(list => Promise.all(list.map(i => i.decode())));
    expect(await images.evaluateAll(list => list.length > 0 && list.every(i => i.complete && i.naturalWidth > 0)), 'every result image decoded with real pixels').toBe(true);
  }
  async expectTitles(titles) { await this.expectCards(titles.map(title => ({ title }))); }
}
