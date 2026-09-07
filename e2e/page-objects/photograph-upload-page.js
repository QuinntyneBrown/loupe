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
  async expectSaving() {
    await expect(this.page.getByRole('status')).toContainText('Saving photograph');
    await expect(this.page.getByRole('button', { name: 'Save photograph', exact: true })).toBeDisabled();
    await expect(this.page).toHaveURL(/\/my-work\/upload$/);
  }
}
