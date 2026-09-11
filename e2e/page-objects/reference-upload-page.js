import { test, expect } from '@playwright/test';
import { open } from 'node:fs/promises';
import AxeBuilder from '@axe-core/playwright';
import { SignInPage } from './sign-in-page.js';

const labels = { title: 'Title (optional)', sourceUrl: 'Source URL (optional)', attribution: 'Attribution (optional)', notes: 'Notes (optional)' };
export class ReferenceUploadPage {
  constructor(page) { this.page = page; }
  async open() { await this.page.goto('/inspiration/upload'); await new SignInPage(this.page).continue(); }
  async expectOpen() { await expect(this.page.getByRole('heading', { name: 'Upload reference', exact: true })).toBeVisible(); }
  async chooseImage() { await this.page.getByLabel('Reference image', { exact: true }).setInputFiles({ name: 'Morning.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') }); }
  async chooseFile(size, mimeType = 'image/png') {
    const input=this.page.getByLabel('Reference image', { exact: true });
    if(size>1000000 && mimeType==='image/png') {
      const path=test.info().outputPath('Image.png'),file=await open(path,'w');
      try { await file.truncate(size); } finally { await file.close(); }
      await input.setInputFiles(path);
    } else await input.setInputFiles({ name: 'Image.png', mimeType, buffer: Buffer.alloc(size) });
  }
  async fill(values) { for (const [key,value] of Object.entries(values)) await this.page.getByLabel(labels[key], { exact: true }).fill(value); }
  async expectDraft(values) { for (const [key,value] of Object.entries(values)) await expect(this.page.getByLabel(labels[key], { exact: true })).toHaveValue(value); }
  async save() { await this.page.getByRole('button', { name: 'Save reference', exact: true }).click(); }
  async retry() { await this.page.getByRole('button', { name: 'Retry upload', exact: true }).click(); }
  async expectDisabled() { await expect(this.page.getByRole('button', { name: /^(Save reference|Retry upload)$/ })).toBeDisabled(); }
  async expectFailure(message = 'The upload was not confirmed. Your fields are still here. Retry to check whether it was saved.') { await expect(this.page.getByRole('alert')).toHaveText(message); await this.expectOpen(); }
  async expectFieldError(field, message) { await expect(this.page.getByLabel(labels[field], { exact: true })).toHaveAttribute('aria-invalid','true'); await this.expectFailure(message); await this.expectDisabled(); }
  async expectFileError(message) { await expect(this.page.getByLabel('Reference image', { exact: true })).toHaveAttribute('aria-invalid','true'); await this.expectFailure(message); await this.expectDisabled(); }
  async expectReselection() { await expect(this.page.getByText('Select the reference image again to retry.', { exact: true })).toBeVisible(); await this.expectDisabled(); }
  async expectSaving() { await expect(this.page.getByRole('status')).toHaveText('Saving reference…'); await this.expectDisabled(); }
  async expectProgress(transferred,total) {
    const progress=this.page.getByRole('progressbar', {name:'Upload progress',exact:true});
    if (total) { await expect(progress).toHaveJSProperty('value',transferred); await expect(progress).toHaveJSProperty('max',total); }
    else await expect(progress).toHaveJSProperty('position',-1);
  }
  async leave() { await this.page.getByRole('main').getByRole('link',{name:'Back to Inspiration',exact:true}).click(); }
  dialog() { return this.page.getByRole('dialog',{name:'Discard unsaved changes?',exact:true}); }
  async expectDiscard() { await expect(this.dialog()).toBeVisible(); await expect(this.dialog().getByRole('button',{name:'Keep editing',exact:true})).toBeFocused(); }
  async expectPendingWarning() { await expect(this.dialog()).toContainText('This upload may finish after you leave. Check Inspiration before uploading it again.'); }
  async keep() { await this.dialog().getByRole('button',{name:'Keep editing',exact:true}).click(); }
  async discard() { await this.dialog().getByRole('button',{name:'Discard',exact:true}).click(); }
  async expectUnload(expected) { expect(await this.page.evaluate(() => !window.dispatchEvent(new Event('beforeunload',{cancelable:true})))).toBe(expected); }
  async expectAccessible() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect((await new AxeBuilder({page:this.page}).withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze()).violations).toEqual([]);
  }
}
