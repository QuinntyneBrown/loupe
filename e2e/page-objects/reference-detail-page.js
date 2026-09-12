import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export class ReferenceDetailPage {
  suggestions() { return this.page.getByRole('region', { name: 'AI suggestions', exact: true }); }
  async expectSuggestions(text) { await expect(this.suggestions()).toContainText(text); }
  async editSuggestion(text) { await this.suggestions().getByLabel('Description', { exact: true }).fill(text); }
  async reviewSuggestions(action) { await this.suggestions().getByRole('button', { name: action, exact: true }).click(); }
  async expectSuggestionError(text) { await expect(this.suggestions().getByRole('alert')).toContainText(text); }
  async undoSuggestions() { await this.suggestions().getByRole('button', { name: 'Undo', exact: true }).click(); }
  async expectPendingTag(name, visible = true) { await expect(this.suggestions().getByRole('button', { name: 'Accept tag ' + name, exact: true })).toHaveCount(visible ? 1 : 0); }
  async editSuggestedTag(name, value, category) { await this.reviewSuggestions('Edit tag ' + name); await this.suggestions().getByLabel('Suggested tag', { exact: true }).fill(value); await this.suggestions().getByLabel('Suggested category', { exact: true }).selectOption(category); }
  async expectSuggestedTagEditor(value) { await expect(this.suggestions().getByLabel('Suggested tag', { exact: true })).toHaveValue(value); }
  async expectSuggestionActionDisabled(name, disabled = true) { const button = this.suggestions().getByRole('button', { name, exact: true }); if (disabled) await expect(button).toBeDisabled(); else await expect(button).toBeEnabled(); }
  async expectSuggestionsAccessible() { expect((await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]); expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); }
  constructor(page) { this.page = page; }
  photographerDialog() {return this.page.getByRole('dialog',{name:'Link a photographer',exact:true});}
  async linkPhotographer() {await this.page.getByRole('button',{name:'Link a photographer',exact:true}).click();}
  async changePhotographer() {await this.page.getByLabel('More actions',{exact:true}).click();await this.page.getByRole('button',{name:'Change photographer',exact:true}).click();}
  async searchPhotographers(query) {await this.photographerDialog().getByRole('searchbox',{name:'Search your photographers',exact:true}).fill(query);}
  async choosePhotographer(name) {await this.photographerDialog().getByRole('radio',{name:new RegExp('^'+name+' ')}).check();}
  async confirmPhotographer() {await this.photographerDialog().getByRole('button',{name:'Link',exact:true}).click();}
  async cancelPhotographer() {await this.photographerDialog().getByRole('button',{name:'Cancel',exact:true}).click();await expect(this.photographerDialog()).not.toBeVisible();}
  async expectPhotographer(name,id) {await expect(this.page.getByRole('link',{name,exact:true}).first()).toHaveAttribute('href','/photographers/'+id);await expect(this.photographerDialog()).not.toBeVisible();}
  async expectPhotographerFailure() {await expect(this.photographerDialog().getByRole('alert')).toBeVisible();}
  async reviewPhotographer() {await this.photographerDialog().getByRole('button',{name:'Review latest reference',exact:true}).click();}
  async newPhotographer(name,url) {await this.photographerDialog().getByRole('textbox',{name:'New photographer name',exact:true}).fill(name);if(url!==undefined)await this.photographerDialog().getByRole('textbox',{name:'Portfolio URL',exact:true}).fill(url);}
  async expectNewPhotographer(name,url) {await expect(this.photographerDialog().getByRole('textbox',{name:'New photographer name',exact:true})).toHaveValue(name);await expect(this.photographerDialog().getByRole('textbox',{name:'Portfolio URL',exact:true})).toHaveValue(url);}
  textEditor(field) { return this.page.getByRole('region', { name: field === 'description' ? 'Description' : 'Your notes', exact: true }); }
  async editText(field, value) { await this.textEditor(field).getByRole('textbox').fill(value); }
  async saveText(field) { await this.textEditor(field).getByRole('button', { name: 'Save', exact: true }).click(); }
  async expectText(field, value) { await expect(this.textEditor(field).getByRole('textbox')).toHaveValue(value); }
  async expectTextSaved(field) { await expect(this.textEditor(field).getByText('Saved', { exact: true })).toBeVisible(); await expect(this.textEditor(field).getByRole('button', { name: 'Save', exact: true })).toBeDisabled(); }
  async expectTextError(field, message) { await expect(this.textEditor(field).getByRole('alert')).toHaveText(message); }
  async reviewText(field) { await this.textEditor(field).getByRole('button', { name: 'Review latest value', exact: true }).click(); }
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
  activeTags() { return this.page.getByRole('region', { name: 'Your tags', exact: true }); }
  async editActiveTag(name, value, category) { await this.activeTags().getByRole('button', { name: 'Edit tag ' + name, exact: true }).click(); await this.activeTags().getByLabel('Tag name', { exact: true }).fill(value); await this.activeTags().getByLabel('Category', { exact: true }).selectOption(category); }
  async saveTagEdit() { await this.activeTags().getByRole('button', { name: 'Save tag', exact: true }).click(); }
  async expectTagEdit(value, category) { await expect(this.activeTags().getByLabel('Tag name', { exact: true })).toHaveValue(value); await expect(this.activeTags().getByLabel('Category', { exact: true })).toHaveValue(category); }
  async expectTagConflict() { await expect(this.activeTags().getByRole('alert')).toContainText('This reference changed.'); }
  async loadLatestTags() { await this.activeTags().getByRole('button', { name: 'Load latest tags', exact: true }).click(); }
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
    await this.expectText('notes', item.notes || '');
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
