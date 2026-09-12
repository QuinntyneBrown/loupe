import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { SignInPage } from './sign-in-page.js';
const labels = { title: 'Title (optional)', sourceUrl: 'Source URL', attribution: 'Attribution (optional)', notes: 'Notes (optional)' };
export class ReferenceLinkPage {
  constructor(page) { this.page = page; }
  async open() { await this.page.goto('/inspiration/link'); await new SignInPage(this.page).continue(); }
  async expectOpen() { await expect(this.page.getByRole('heading',{name:'Save link',exact:true})).toBeVisible(); }
  async fill(values) { for (const [key,value] of Object.entries(values)) await this.page.getByLabel(labels[key], { exact: true }).fill(value); }
  async expectDraft(values) { for (const [key,value] of Object.entries(values)) await expect(this.page.getByLabel(labels[key], { exact: true })).toHaveValue(value); }
  async save() { await this.page.getByRole('button', { name: 'Save link', exact: true }).click(); }
  async retry() { await this.page.getByRole('button', { name: 'Retry save', exact: true }).click(); }
  async expectDisabled() { await expect(this.page.getByRole('button', { name: /^(Save link|Retry save)$/ })).toBeDisabled(); }
  async expectFailure(message = 'The link save was not confirmed. Your fields are still here. Retry to check whether it was saved.') { await expect(this.page.getByRole('alert')).toHaveText(message); await this.expectOpen(); }
  async expectFieldError(field, message) { await expect(this.page.getByLabel(labels[field], { exact: true })).toHaveAttribute('aria-invalid','true'); await this.expectFailure(message); await this.expectDisabled(); }
  async expectSaving() { await expect(this.page.getByRole('status')).toHaveText('Saving link…'); await this.expectDisabled(); }
  async expectDuplicate() { await expect(this.page.getByRole('heading',{name:'Already saved',exact:true})).toBeFocused(); }
  async openExisting() { await this.page.getByRole('link',{name:'Open saved reference',exact:true}).click(); }
  async leave() { await this.page.getByRole('main').getByRole('link',{name:'Back to Inspiration',exact:true}).click(); }
  dialog() { return this.page.getByRole('dialog',{name:'Discard unsaved changes?',exact:true}); }
  async expectDiscard() { await expect(this.dialog()).toBeVisible(); await expect(this.dialog().getByRole('button',{name:'Keep editing',exact:true})).toBeFocused(); }
  async expectPendingWarning() { await expect(this.dialog()).toContainText('This save may finish after you leave. Check Inspiration before saving it again.'); }
  async keep() { await this.dialog().getByRole('button',{name:'Keep editing',exact:true}).click(); }
  async discard() { await this.dialog().getByRole('button',{name:'Discard',exact:true}).click(); }
  async expectUnload(expected) { expect(await this.page.evaluate(() => !window.dispatchEvent(new Event('beforeunload',{cancelable:true})))).toBe(expected); }
  async expectAccessible() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect((await new AxeBuilder({page:this.page}).withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze()).violations).toEqual([]);
  }
}
