import { expect } from '@playwright/test';

export class ReferenceDetailPage {
  constructor(page) { this.page = page; }
  async expectTitle() { await expect(this.page).toHaveTitle('Reference \u00b7 Loupe'); }
  async expectHeadingFocus(title) { await expect(this.page.getByRole('heading', { name: title, exact: true })).toBeFocused(); }
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
