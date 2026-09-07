import { expect } from '@playwright/test';

export class ReferencePage {
  constructor(page) { this.page = page; }
  async open() { await this.page.goto('/'); }
  async expectReady() {
    await expect(this.page.getByRole('heading', { name: 'A clear frame for photography.', exact: true })).toBeVisible();
  }
  async activatePrimaryWithKeyboard() {
    await this.page.getByRole('button', { name: 'Try primary button', exact: true }).focus();
    await this.page.keyboard.press('Enter');
  }
  async expectPrimaryFeedback() {
    await expect(this.page.getByRole('status')).toHaveText('Primary button activated.');
  }
  async expectDisabledExample() {
    await expect(this.page.getByRole('button', { name: 'Unavailable example' })).toBeDisabled();
  }
}
