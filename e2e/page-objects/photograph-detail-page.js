import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export class PhotographDetailPage {
  constructor(page) { this.page = page; }
  critique() { return this.page.getByRole('region', { name: 'Photo critique', exact: true }); }
  async expectCritique(mode = 'Live') {
    const critique = this.critique();
    await expect(critique.getByRole('heading', { name: 'Strengths', exact: true })).toBeVisible();
    await expect(critique).toContainText('The shape communicates the intended quiet mood.');
    for (const heading of ['Exposure', 'Focus', 'Depth of field', 'Motion', 'Lighting', 'Color', 'Processing', 'Framing', 'Subject separation', 'Balance', 'Visual hierarchy', 'Mood'])
      await expect(critique.getByRole('heading', { name: heading, exact: true })).toBeVisible();
    await expect(critique).toContainText('The preview cannot establish focus with confidence.');
    for (const label of ['Visible observation', 'EXIF fact', 'Hypothesis', 'Stylistic preference'])
      await expect(critique.getByText(label, { exact: true }).first()).toBeVisible();
    for (const number of [1, 2, 3]) {
      await expect(critique).toContainText(`Observed edge ${number}`);
      await expect(critique).toContainText(`Attention effect ${number}`);
      await expect(critique).toContainText(`Try framing change ${number}`);
    }
    await expect(critique).toContainText('Make two frames with different subject positions.');
    await expect(critique).toContainText('Compare which silhouette is easier to distinguish.');
    await expect(critique.locator('time')).toHaveAttribute('datetime', '2026-09-07T13:00:00Z');
    await expect(critique).toContainText(mode === 'Demo' ? 'Demo · illustrative sample' : 'AI-generated critique');
    await expect(critique).toContainText(mode === 'Demo' ? 'loupe-demo-critique-v1' : 'gpt-5.4-mini-2026-03-17');
    await expect(critique).toContainText('critique-v1');
    await expect(critique).toContainText('Make deliberate silhouettes');
    await expect(critique).toContainText('Preserve the strong shapes');
    await expect(critique).not.toContainText('Explore quiet morning light');
  }
  async expectNoCritique() { await expect(this.critique()).toContainText('No critique saved yet.'); }
  async expectCritiqueLoading() { await expect(this.critique().getByRole('status')).toHaveText('Loading critique…'); }
  async expectCritiqueFailure() { await expect(this.critique().getByRole('alert')).toHaveText('The saved critique could not be loaded. Try again.'); }
  async retryCritique() { await this.critique().getByRole('button', { name: 'Retry loading critique', exact: true }).click(); }
  async expectCritiqueFocus() { await expect(this.critique().getByRole('heading', { name: 'Photo critique', exact: true })).toBeFocused(); }
  deleteDialog() { return this.page.getByRole('dialog', { name: 'Delete “Study 01”?', exact: true }); }
  async deletePhotograph() { await this.page.getByRole('button', { name: 'Delete photograph', exact: true }).click(); }
  async expectDeleteConfirmation() {
    await expect(this.deleteDialog()).toBeVisible();
    await expect(this.deleteDialog()).toContainText('images, critique, notes, and capture settings');
    await expect(this.deleteDialog()).toContainText('Any unsaved changes will be discarded.');
    await expect(this.deleteDialog().getByRole('button', { name: 'Cancel', exact: true })).toBeFocused();
  }
  async cancelDeletion() { await this.deleteDialog().getByRole('button', { name: 'Cancel', exact: true }).click(); }
  async confirmDeletion() { await this.deleteDialog().getByRole('button', { name: 'Delete photograph', exact: true }).click(); }
  async expectDeletionCancelled() {
    await expect(this.deleteDialog()).toHaveCount(0);
    await expect(this.page.getByRole('button', { name: 'Delete photograph', exact: true })).toBeFocused();
  }
  async expectDeleting() {
    await expect(this.deleteDialog().getByRole('status')).toHaveText('Deleting photograph…');
    await expect(this.deleteDialog().getByRole('button', { name: 'Delete photograph', exact: true })).toBeDisabled();
    await expect(this.deleteDialog().getByRole('button', { name: 'Cancel', exact: true })).toBeDisabled();
  }
  async expectDeletionFailure() {
    await expect(this.deleteDialog().getByRole('alert')).toHaveText('Deletion was not confirmed. Retry to check its status.');
  }
  async retryDeletion() { await this.deleteDialog().getByRole('button', { name: 'Retry deletion', exact: true }).click(); }
  async expectDeletionConflict() {
    await expect(this.deleteDialog().getByRole('alert')).toContainText('This photograph changed. Review its latest saved details before deleting.');
    await expect(this.deleteDialog().getByRole('button', { name: 'Delete photograph', exact: true })).toHaveCount(0);
    await expect(this.deleteDialog().getByRole('button', { name: 'Retry deletion', exact: true })).toHaveCount(0);
  }
  async reviewDeletion() { await this.deleteDialog().getByRole('button', { name: 'Review latest photograph', exact: true }).click(); }
  async expectDeletionReview(notes, intent) {
    const review = this.deleteDialog().getByRole('region', { name: 'Latest saved details', exact: true });
    await expect(review).toContainText(notes);
    await expect(review).toContainText(intent);
    await expect(this.deleteDialog().getByRole('button', { name: 'Delete photograph', exact: true })).toBeEnabled();
  }
  async expectDeletionReviewFailure() { await expect(this.deleteDialog().getByRole('alert')).toContainText('Latest details could not be loaded. Your unsaved changes are still here.'); }
  async expectDeletionUnavailable() {
    await expect(this.deleteDialog().getByRole('alert')).toHaveText('This photograph is no longer available. Close this dialog to return to your library or keep your unsaved text.');
    await expect(this.deleteDialog().getByRole('button', { name: /Delete photograph|Retry deletion|Review latest photograph/ })).toHaveCount(0);
  }
  async expectDeletionKeyboard() {
    await this.expectDeleteConfirmation();
    await this.page.keyboard.press('Tab');
    await expect(this.deleteDialog().getByRole('button', { name: 'Delete photograph', exact: true })).toBeFocused();
    await this.page.keyboard.press('Tab');
    await expect(this.deleteDialog().getByRole('button', { name: 'Cancel', exact: true })).toBeFocused();
    await this.page.keyboard.press('Shift+Tab');
    await expect(this.deleteDialog().getByRole('button', { name: 'Delete photograph', exact: true })).toBeFocused();
  }
  async escapeDeletion() { await this.page.keyboard.press('Escape'); }
  async expectPendingDeletionKeyboard() {
    await this.page.keyboard.press('Tab');
    await expect(this.deleteDialog()).toBeFocused();
    await this.page.keyboard.press('Escape');
    await this.expectDeleting();
  }
  async expectDeletionReviewFocus() { await expect(this.deleteDialog().getByRole('heading', { name: 'Latest saved details', exact: true })).toBeFocused(); }
  async expectReviewActionsReachable() {
    await this.page.keyboard.press('Tab');
    const cancel = this.deleteDialog().getByRole('button', { name: 'Cancel', exact: true });
    await expect(cancel).toBeFocused();
    await expect(cancel).toBeInViewport({ ratio: 1 });
    await this.page.keyboard.press('Shift+Tab');
    const confirm = this.deleteDialog().getByRole('button', { name: 'Delete photograph', exact: true });
    await expect(confirm).toBeFocused();
    await expect(confirm).toBeInViewport({ ratio: 1 });
  }
  async expectAccessibleDeletion() {
    const box = await this.deleteDialog().boundingBox();
    const viewport = this.page.viewportSize();
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
    for (const button of await this.deleteDialog().getByRole('button').all()) {
      const target = await button.boundingBox();
      expect(target.width).toBeGreaterThanOrEqual(24);
      expect(target.height).toBeGreaterThanOrEqual(24);
    }
    await this.expectAccessibleBrief();
  }
  async expectImage(title) {
    await expect(this.page.getByRole('heading', { level: 1, name: title, exact: true })).toBeVisible();
    await expect(this.page.getByRole('img', { name: title, exact: true })).toBeVisible();
  }
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
  async expectBriefConflict() { await expect(this.brief().getByRole('alert')).toHaveText('This photograph changed. Your brief has been kept.'); }
  async reloadLatestBrief() { await this.brief().getByRole('button', { name: 'Reload latest brief', exact: true }).click(); }
  async expectLatestBrief(values) {
    const latest = this.brief().getByRole('region', { name: 'Latest saved brief', exact: true });
    for (const value of Object.values(values)) await expect(latest.getByText(value, { exact: true })).toBeVisible();
  }
  async expectBriefReloadFailure() { await expect(this.brief().getByRole('alert')).toHaveText('Latest brief could not be loaded. Your changes are still here.'); }
  async returnToLibrary() { await this.page.getByRole('link', { name: 'My Work', exact: true }).click(); }
  discardDialog() { return this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true }); }
  async expectDiscardChoice() {
    await expect(this.discardDialog()).toBeVisible();
    await expect(this.discardDialog().getByRole('button', { name: 'Keep editing', exact: true })).toBeFocused();
  }
  async keepEditing() { await this.discardDialog().getByRole('button', { name: 'Keep editing', exact: true }).click(); }
  async discardChanges() { await this.discardDialog().getByRole('button', { name: 'Discard', exact: true }).click(); }
  async expectNoDiscardChoice() { await expect(this.discardDialog()).toHaveCount(0); }
  async escapeDiscard() { await this.page.keyboard.press('Escape'); }
  async expectUnloadProtection(expected) {
    const prevented = await this.page.evaluate(() => !window.dispatchEvent(new Event('beforeunload', { cancelable: true })));
    expect(prevented).toBe(expected);
  }
  async expectBriefSaving() {
    await expect(this.brief().getByRole('status')).toHaveText('Saving brief…');
    await expect(this.brief().getByRole('button', { name: 'Save brief', exact: true })).toBeDisabled();
  }
  async expectAccessibleBrief() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
  async expectBriefReviewFocus() { await expect(this.brief().getByRole('heading', { name: 'Latest saved brief', exact: true })).toBeFocused(); }
  async expectNotesReviewFocus() { await expect(this.page.getByRole('heading', { name: 'Latest saved notes', exact: true })).toBeFocused(); }
  async expectDiscardKeyboardAndLayout() {
    await this.expectDiscardChoice();
    await this.page.keyboard.press('Tab');
    await expect(this.discardDialog().getByRole('button', { name: 'Discard', exact: true })).toBeFocused();
    await this.page.keyboard.press('Tab');
    await expect(this.discardDialog().getByRole('button', { name: 'Keep editing', exact: true })).toBeFocused();
    await this.page.keyboard.press('Shift+Tab');
    await expect(this.discardDialog().getByRole('button', { name: 'Discard', exact: true })).toBeFocused();
    const box = await this.discardDialog().boundingBox();
    const viewport = this.page.viewportSize();
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
    await this.expectAccessibleBrief();
  }
}
