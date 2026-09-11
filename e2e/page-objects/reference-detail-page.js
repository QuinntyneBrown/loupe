import { expect } from '@playwright/test';

export class ReferenceDetailPage {
  constructor(page) { this.page = page; }
  async openBoards() { await this.page.getByRole('button', { name: 'Add to boards', exact: true }).click(); }
  async selectPickerBoard(name) { await this.page.getByRole('dialog', { name: 'Add to boards', exact: true }).getByRole('checkbox', { name, exact: true }).check(); }
  async expectBoardLink(name) { await expect(this.page.getByRole('region', { name: 'Boards', exact: true }).getByRole('link', { name, exact: true })).toBeVisible(); }
  async followBoard(name) { await this.page.getByRole('region', { name: 'Boards', exact: true }).getByRole('link', { name, exact: true }).click(); }
  async expectBoardButtonFocus() { await expect(this.page.getByRole('button', { name: 'Add to boards', exact: true })).toBeFocused(); }
  async openDelete() { await this.page.getByLabel('More actions', { exact: true }).click(); await this.page.getByRole('button', { name: 'Delete reference', exact: true }).click(); await expect(this.deleteDialog().getByRole('button', { name: 'Cancel', exact: true })).toBeFocused(); }
  deleteDialog() { return this.page.getByRole('dialog', { name: 'Delete this reference?', exact: true }); }
  async cancelDelete() { await this.deleteDialog().getByRole('button', { name: 'Cancel', exact: true }).click(); await expect(this.deleteDialog()).toHaveCount(0); await expect(this.page.getByLabel('More actions', { exact: true })).toBeFocused(); }
  async confirmDelete() { await this.deleteDialog().getByRole('button', { name: 'Delete', exact: true }).click(); }
  async expectDeleteError(message) { await expect(this.deleteDialog().getByRole('alert')).toHaveText(message); }
  async expectDeleteDisabled() { await expect(this.deleteDialog().getByRole('button', { name: 'Delete', exact: true })).toBeDisabled(); }
  async reviewDeletion() { await this.deleteDialog().getByRole('button', { name: 'Review latest reference', exact: true }).click(); }
  async expectDeletionComplete() { await expect(this.page).toHaveURL(/\/inspiration$/); await expect(this.page.getByRole('status')).toContainText('Reference deleted.'); }
  async openImageReplacement() { await this.page.getByLabel('More actions', { exact: true }).click(); await this.page.getByRole('button', { name: 'Replace image', exact: true }).click(); }
  imageDialog() { return this.page.getByRole('dialog', { name: 'Add an image', exact: true }); }
  async chooseReplacement(type = 'image/png') { await this.imageDialog().getByLabel('Choose an image', { exact: true }).setInputFiles({ name: 'Replacement.png', mimeType: type, buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1kAAAAASUVORK5CYII=', 'base64') }); }
  async saveImage() { await this.imageDialog().getByRole('button', { name: 'Save image', exact: true }).click(); }
  async cancelImage() { await this.imageDialog().getByRole('button', { name: 'Cancel', exact: true }).click(); }
  async expectImageDialogClosed() { await expect(this.imageDialog()).toHaveCount(0); await expect(this.page.getByLabel('More actions', { exact: true })).toBeFocused(); }
  async expectImageError(message) { await expect(this.imageDialog().getByRole('alert')).toHaveText(message); }
  async expectImageSaveDisabled() { await expect(this.imageDialog().getByRole('button', { name: 'Save image', exact: true })).toBeDisabled(); }
  async reloadImageReference() { await this.imageDialog().getByRole('button', { name: 'Review latest reference', exact: true }).click(); }
  async addTag(name) { const input = this.page.getByRole('textbox', { name: 'Add a tag', exact: true }); await input.fill(name); await input.press('Enter'); }
  async removeTag(name) { await this.page.getByRole('button', { name: `Remove tag ${name}`, exact: true }).click(); }
  async expectTag(name) { await expect(this.page.getByRole('button', { name: `Remove tag ${name}`, exact: true })).toBeVisible(); }
  async expectNoTag(name) { await expect(this.page.getByRole('button', { name: `Remove tag ${name}`, exact: true })).toHaveCount(0); }
  async expectTagFailure() { await expect(this.page.getByRole('region', { name: 'Your tags', exact: true }).getByRole('alert')).toHaveText('Tags could not be saved. Your change is still here. Try again.'); }
  async retryTags() { await this.page.getByRole('button', { name: 'Retry saving tags', exact: true }).click(); }
  async expectTitle() { await expect(this.page).toHaveTitle('Reference \u00b7 Loupe'); }
  async expectHeadingFocus(title) { await expect(this.page.getByRole('heading', { name: title, exact: true })).toBeFocused(); }
  async edit() { await this.page.getByRole('button',{name:'Edit metadata',exact:true}).click(); await expect(this.page.getByLabel('Title',{exact:true})).toBeFocused(); }
  async fillMetadata(values) { for(const [key,value] of Object.entries(values)) await this.page.getByLabel(this.metadataLabel(key),{exact:true}).fill(value); }
  importRegion() {return this.page.getByRole('region',{name:'Source import',exact:true});}
  async requestImport() {await this.importRegion().getByRole('button',{name:'Request import',exact:true}).click();}
  async retryImportAdmission() {await this.importRegion().getByRole('button',{name:'Retry import request',exact:true}).click();}
  async expectImportReady() {await expect(this.importRegion().getByRole('button',{name:'Request import',exact:true})).toBeEnabled();}
  async expectImportDisabled() {await expect(this.importRegion().getByRole('button',{name:/^(Request import|Retry import request)$/})).toBeDisabled();}
  async expectNoImportSource() {await expect(this.importRegion()).toContainText('Add a source URL in metadata to request an import.');}
  async expectImportState(state) {await expect(this.importRegion().getByText(state,{exact:true})).toBeVisible();}
  async expectImportMessage(text) {await expect(this.importRegion()).toContainText(text);}
  async expectImportError(text) {await expect(this.importRegion().getByRole('alert')).toContainText(text);}
  async expectImportFocused() {await expect(this.importRegion().getByRole('heading',{name:'Source import',exact:true})).toBeFocused();}
  async retryImportStatus() {await this.importRegion().getByRole('button',{name:'Retry loading import status',exact:true}).click();}
  async reviewImportSource() {await this.importRegion().getByRole('button',{name:'Review latest source',exact:true}).click();}
  async expectReviewedImportSource(source) {await expect(this.importRegion()).toContainText(source);}
  async expectMetadataFieldFocused(key) {await expect(this.page.getByLabel(this.metadataLabel(key),{exact:true})).toBeFocused();}
  metadataLabel(key) { return {title:'Title',sourceUrl:'Source URL (optional)',attribution:'Attribution (optional)',notes:'Notes (optional)'}[key]; }
  async expectDraft(values) { for(const [key,value] of Object.entries(values)) await expect(this.page.getByLabel(this.metadataLabel(key),{exact:true})).toHaveValue(value); }
  async saveMetadata() { await this.page.getByRole('button',{name:/^(Save metadata|Retry metadata save)$/}).click(); }
  async expectMetadataClosed() { await expect(this.page.getByRole('button',{name:'Edit metadata',exact:true})).toBeFocused(); }
  async expectMetadataDisabled() { await expect(this.page.getByRole('button',{name:/^(Save metadata|Retry metadata save)$/})).toBeDisabled(); }
  async expectMetadataError(message) { await expect(this.page.getByRole('alert')).toHaveText(message); }
  async expectMetadataFieldError(key,message) { await expect(this.page.getByLabel(this.metadataLabel(key),{exact:true})).toHaveAttribute('aria-invalid','true'); await this.expectMetadataError(message); await this.expectMetadataDisabled(); }
  async reloadMetadata() { await this.page.getByRole('button',{name:'Reload latest metadata',exact:true}).click(); }
  async expectLatestMetadata(values) { const latest=this.page.getByRole('region',{name:'Latest saved metadata',exact:true}); await expect(latest.getByRole('heading',{name:'Latest saved metadata',exact:true})).toBeFocused(); for(const value of Object.values(values)) await expect(latest).toContainText(value||'Not specified'); }
  async cancelMetadata() { await this.page.getByRole('button',{name:'Cancel metadata edit',exact:true}).click(); }
  async expectMetadataSaving() { await expect(this.page.getByRole('status')).toHaveText('Saving metadata…'); await this.expectMetadataDisabled(); }
  metadataDialog() { return this.page.getByRole('dialog',{name:'Discard unsaved changes?',exact:true}); }
  async expectMetadataDiscard() { await expect(this.metadataDialog()).toBeVisible(); await expect(this.metadataDialog().getByRole('button',{name:'Keep editing',exact:true})).toBeFocused(); }
  async keepMetadata() { await this.metadataDialog().getByRole('button',{name:'Keep editing',exact:true}).click(); }
  async discardMetadata() { await this.metadataDialog().getByRole('button',{name:'Discard',exact:true}).click(); }
  async expectUnload(expected) { expect(await this.page.evaluate(()=>!window.dispatchEvent(new Event('beforeunload',{cancelable:true})))).toBe(expected); }
  async expectSaved(item) {
    await expect(this.page.getByRole('heading', { name: item.title, exact: true })).toBeVisible();
    const main = this.page.getByRole('main');
    if (item.imageUrl) {
      const image = main.getByRole('img', { name: item.title, exact: true });
      await expect(image).toHaveAttribute('src', item.imageUrl);
      const box = await image.boundingBox(); expect(Math.abs(box.width / box.height - item.width / item.height)).toBeLessThan(0.02);
    } else { await expect(main.getByRole('img')).toHaveCount(0); await expect(main.getByText('Link-only reference', { exact: true })).toBeVisible(); }
    await expect(main).toContainText(item.attribution || 'Attribution unknown');
    await expect(main).toContainText(item.notes || 'No notes saved.');
    if (item.sourceUrl) await expect(main.getByRole('link', { name: 'Open source', exact: true })).toHaveAttribute('href', item.sourceUrl);
    else await expect(main).toContainText('Source unknown');
    await expect(main.locator('time')).toHaveAttribute('datetime', item.createdAt);
  }
  async openSourceSafely(url) {
    await this.page.context().route('https://source.example/**', route => route.fulfill({ contentType: 'text/html', body: '<p>Controlled source</p>' }));
    const pending = this.page.waitForEvent('popup');
    await this.page.getByRole('link', { name: 'Open source', exact: true }).click();
    const source = await pending; await source.waitForLoadState();
    await expect(source).toHaveURL(url);
    expect(await source.evaluate(() => window.opener)).toBeNull();
    expect(await source.evaluate(() => document.referrer)).toBe('');
    await source.close();
  }
  async expectUnavailable() { await expect(this.page.getByRole('alert')).toHaveText('Reference unavailable. It may have been removed.'); }
  async expectFailure() { await expect(this.page.getByRole('alert')).toHaveText('The reference could not be loaded. Try again.'); }
  async retry() { await this.page.getByRole('button', { name: 'Retry loading reference', exact: true }).click(); }
  async returnToLibrary() { await this.page.getByRole('link', { name: 'Back to Inspiration', exact: true }).click(); }
}
