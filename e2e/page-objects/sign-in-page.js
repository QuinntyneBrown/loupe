import { expect } from '@playwright/test';

export class SignInPage {
  constructor(page) { this.page = page; }
  async openPrivateDestination() { await this.page.goto('/my-work'); }
  async expectSignInRequired() {
    await expect(this.page.getByRole('heading', { name: 'A private space for your photography' })).toBeVisible();
    await expect(this.page).toHaveURL(/\/sign-in\?returnUrl=%2Fmy-work$/);
  }
  async continue() { await this.page.getByRole('button', { name: 'Continue to sign in' }).click(); }
  async expectSignedOut() {
    await expect(this.page.getByRole('heading', { name: 'A private space for your photography' })).toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Sign out', exact: true })).toHaveCount(0);
  }
  async openFailedCallback() { await this.page.goto('/sign-in?error=authentication_failed'); }
  async expectRetryableFailure() {
    await expect(this.page.getByRole('alert')).toHaveText('Sign-in could not be completed. Please try again.');
    await expect(this.page.getByRole('button', { name: 'Continue to sign in' })).toBeEnabled();
  }
  async openWithDestination(destination) { await this.page.goto(`/sign-in?returnUrl=${encodeURIComponent(destination)}`); }
}
