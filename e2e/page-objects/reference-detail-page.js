import { expect } from '@playwright/test';

export class ReferenceDetailPage {
  constructor(page) { this.page = page; }
  async expectTitle() { await expect(this.page).toHaveTitle('Reference \u00b7 Loupe'); }
  async expectHeadingFocus(title) { await expect(this.page.getByRole('heading', { name: title, exact: true })).toBeFocused(); }
  async edit() { await this.page.getByRole('button',{name:'Edit metadata',exact:true}).click(); await expect(this.page.getByLabel('Title',{exact:true})).toBeFocused(); }
  async fillMetadata(values) { for(const [key,value] of Object.entries(values)) await this.page.getByLabel(this.metadataLabel(key),{exact:true}).fill(value); }
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
