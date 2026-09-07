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
    await this.expectNotes('Keep the edges quiet.\nTry a lower viewpoint.');
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
    await this.expectNotes('');
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
  async editNotes(value) { await this.page.getByRole('textbox', { name: 'Notes', exact: true }).fill(value); }
  async expectNotes(value) { await expect(this.page.getByRole('textbox', { name: 'Notes', exact: true })).toHaveValue(value); }
  async saveNotes() { await this.page.getByRole('button', { name: 'Save notes', exact: true }).click(); }
  async expectNotesState(value) { await expect(this.page.getByRole('region', { name: 'Personal notes', exact: true }).getByRole('status')).toHaveText(value); }
  async expectNotesFailure() {
    await expect(this.page.getByRole('alert')).toHaveText('Notes could not be saved. Your text is still here.');
    await expect(this.page.getByRole('button', { name: 'Retry save', exact: true })).toBeEnabled();
  }
  async retryNotes() { await this.page.getByRole('button', { name: 'Retry save', exact: true }).click(); }
  async expectNotesLimit() {
    await expect(this.page.getByText('Use 10,000 characters or fewer.', { exact: true })).toBeVisible();
    await expect(this.page.getByRole('textbox', { name: 'Notes', exact: true })).toHaveAttribute('aria-invalid', 'true');
  }
  async expectAccessibleNotes() {
    const editor = this.page.getByRole('textbox', { name: 'Notes', exact: true });
    await editor.scrollIntoViewIfNeeded();
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
  async expectNotesConflict() {
    await expect(this.page.getByRole('alert')).toHaveText('This photograph changed. Your notes have been kept.');
  }
  async reloadLatestNotes() { await this.page.getByRole('button', { name: 'Reload latest notes', exact: true }).click(); }
  async expectLatestNotes(value) { await expect(this.page.getByRole('region', { name: 'Latest saved notes', exact: true })).toContainText(value); }
  async expectNotesReloadFailure() { await expect(this.page.getByRole('alert')).toContainText('Latest notes could not be loaded. Your text is still here.'); }
  brief() { return this.page.getByRole('region', { name: 'Critique brief', exact: true }); }
  async editBrief() { await this.brief().getByRole('button', { name: 'Edit brief', exact: true }).click(); }
  async fillBrief(values) {
    const labels = { intent: 'Intent', genre: 'Genre', requestedFeedback: 'Requested feedback' };
    for (const [field, value] of Object.entries(values)) {
      if (field === 'experience') await this.brief().getByLabel('Experience', { exact: true }).selectOption(value);
      else await this.brief().getByLabel(labels[field], { exact: true }).fill(value);
    }
  }
  async saveBrief() { await this.brief().getByRole('button', { name: 'Save brief', exact: true }).click(); }
  async expectBriefValues(values) {
    for (const value of Object.values(values)) await expect(this.brief().getByText(value, { exact: true })).toBeVisible();
  }
  async expectBriefDraft(values) {
    const labels = { intent: 'Intent', genre: 'Genre', experience: 'Experience', requestedFeedback: 'Requested feedback' };
    for (const [field, value] of Object.entries(values)) await expect(this.brief().getByLabel(labels[field], { exact: true })).toHaveValue(value);
  }
  async expectNoBrief() { await expect(this.brief().getByText('No brief added.', { exact: true })).toBeVisible(); }
  async expectBriefFailure() { await expect(this.brief().getByRole('alert')).toHaveText('The brief could not be saved. Your changes are still here.'); }
  async retryBrief() { await this.brief().getByRole('button', { name: 'Retry brief save', exact: true }).click(); }
  async cancelBrief() { await this.brief().getByRole('button', { name: 'Cancel brief edit', exact: true }).click(); }
  async expectBriefLimit(maximum = 2000) { await expect(this.brief().getByText(`Use ${maximum.toLocaleString('en-US')} characters or fewer.`, { exact: true })).toBeVisible(); }
}
