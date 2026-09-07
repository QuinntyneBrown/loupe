import { expect } from '@playwright/test';

export class PhotographUploadPage {
  constructor(page) { this.page = page; }
  async open() { await this.page.getByRole('link', { name: 'Upload photograph', exact: true }).click(); }
  async expectOpen() { await expect(this.page.getByRole('heading', { name: 'Upload photograph', exact: true })).toBeVisible(); }
  async chooseImage(name = 'Morning.png') {
    await this.page.getByLabel('Photograph', { exact: true }).setInputFiles({ name, mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') });
  }
  async fill(values) {
    const labels = { title: 'Title (optional)', intent: 'Intent (optional)', genre: 'Genre (optional)', requestedFeedback: 'Requested feedback (optional)' };
    for (const [field, value] of Object.entries(values)) {
      if (field === 'experience') await this.page.getByLabel('Experience (optional)', { exact: true }).selectOption(value);
      else await this.page.getByLabel(labels[field], { exact: true }).fill(value);
    }
  }
  async save() { await this.page.getByRole('button', { name: 'Save photograph', exact: true }).click(); }
  async chooseFile({ name = 'Morning.png', mimeType = 'image/png', size = 100 } = {}) {
    await this.page.getByLabel('Photograph', { exact: true }).setInputFiles({ name, mimeType, buffer: Buffer.alloc(size) });
  }
  async expectFieldLimit(field, maximum) {
    const labels = { title: 'Title (optional)', intent: 'Intent (optional)', genre: 'Genre (optional)', requestedFeedback: 'Requested feedback (optional)' };
    await expect(this.page.getByLabel(labels[field], { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await expect(this.page.getByRole('alert')).toHaveText(`Use ${maximum.toLocaleString('en-US')} characters or fewer.`);
    await this.expectSaveDisabled();
  }
  async expectFileError(message) {
    await expect(this.page.getByLabel('Photograph', { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await expect(this.page.getByRole('alert')).toHaveText(message);
    await this.expectSaveDisabled();
  }
  async expectSaveDisabled() { await expect(this.page.getByRole('button', { name: 'Save photograph', exact: true })).toBeDisabled(); }
  async expectFailure(message = 'The upload was not confirmed. Your fields are still here. Retry to check whether it was saved.') {
    await expect(this.page.getByRole('alert')).toHaveText(message);
    await expect(this.page).toHaveURL(/\/my-work\/upload$/);
  }
  async expectDraft(values) {
    const labels = { title: 'Title (optional)', intent: 'Intent (optional)', genre: 'Genre (optional)', experience: 'Experience (optional)', requestedFeedback: 'Requested feedback (optional)' };
    for (const [field, value] of Object.entries(values)) await expect(this.page.getByLabel(labels[field], { exact: true })).toHaveValue(value);
  }
  async expectFileRetained() { await expect(this.page.getByText('The selected file is still available.', { exact: true })).toBeVisible(); }
  async expectReselectionRequired() {
    await expect(this.page.getByText('Select the photograph again to retry.', { exact: true })).toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Retry upload', exact: true })).toBeDisabled();
  }
  async retry() { await this.page.getByRole('button', { name: 'Retry upload', exact: true }).click(); }
  async expectProgress(transferred, total) {
    const progress = this.page.getByRole('progressbar', { name: 'Upload progress', exact: true });
    await expect(progress).toHaveJSProperty('value', transferred);
    await expect(progress).toHaveJSProperty('max', total);
    await expect(this.page.getByRole('status')).toHaveText(`${transferred.toLocaleString('en-US')} of ${total.toLocaleString('en-US')} bytes transferred.`);
  }
  async expectIndeterminate(transferred = null) {
    await expect(this.page.getByRole('progressbar', { name: 'Upload progress', exact: true })).toHaveJSProperty('position', -1);
    await expect(this.page.getByRole('status')).toHaveText(transferred === null ? 'Saving photograph…' : `${transferred.toLocaleString('en-US')} bytes transferred. Total size unavailable.`);
  }
  async expectNoProgress() { await expect(this.page.getByRole('progressbar', { name: 'Upload progress', exact: true })).toHaveCount(0); }
  async returnToLibrary() { await this.page.getByRole('link', { name: 'My Work', exact: true }).click(); }
  discardDialog() { return this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true }); }
  async expectDiscardChoice() {
    await expect(this.discardDialog()).toBeVisible();
    await expect(this.discardDialog().getByRole('button', { name: 'Keep editing', exact: true })).toBeFocused();
  }
  async expectPendingWarning() { await expect(this.discardDialog()).toContainText('This upload may finish after you leave. Check My Work before uploading it again.'); }
  async keepEditing() { await this.discardDialog().getByRole('button', { name: 'Keep editing', exact: true }).click(); }
  async discardChanges() { await this.discardDialog().getByRole('button', { name: 'Discard', exact: true }).click(); }
  async escapeDiscard() { await this.page.keyboard.press('Escape'); }
  async expectNoDiscardChoice() { await expect(this.discardDialog()).toHaveCount(0); }
  async expectUnloadProtection(expected) {
    expect(await this.page.evaluate(() => !window.dispatchEvent(new Event('beforeunload', { cancelable: true })))).toBe(expected);
  }
  async expectEmptyDraft() {
    await this.expectDraft({ title: '', intent: '', genre: '', experience: '', requestedFeedback: '' });
    await expect(this.page.getByLabel('Photograph', { exact: true })).toHaveValue('');
    await this.expectSaveDisabled();
  }
  async expectSaving() {
    await expect(this.page.getByRole('status')).toContainText('Saving photograph');
    await expect(this.page.getByRole('button', { name: 'Save photograph', exact: true })).toBeDisabled();
    await expect(this.page).toHaveURL(/\/my-work\/upload$/);
  }
}
