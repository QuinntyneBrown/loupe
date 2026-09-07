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
  async expectSaving() {
    await expect(this.page.getByRole('status')).toContainText('Saving photograph');
    await expect(this.page.getByRole('button', { name: 'Save photograph', exact: true })).toBeDisabled();
    await expect(this.page).toHaveURL(/\/my-work\/upload$/);
  }
}
