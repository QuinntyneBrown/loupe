import { expect } from '@playwright/test';

export class LocationsPage {
  constructor(page) {
    this.page = page;
    this.requests = [];
    page.on('request', (request) => this.requests.push(request.url()));
  }
  async open() { await this.page.goto('/locations.html'); }
  async openFromReference() {
    await this.page.goto('/');
    await this.page.getByRole('link', { name: 'Locations', exact: true }).click();
    await expect(this.page).toHaveURL(/\/locations\.html$/);
  }
  async expectReady() {
    await expect(this.page.getByRole('heading', { level: 1, name: 'Places, seen before the shoot.', exact: true })).toBeVisible();
    await expect(this.page.getByText('No application, API, or external images are connected.')).toBeVisible();
  }
  cards() { return this.page.getByRole('list', { name: 'Location cards example', exact: true }).getByRole('listitem'); }
  async expectCards(entries) {
    await expect(this.cards()).toHaveCount(entries.length);
    for (const [name, status] of entries) {
      const card = this.cards().filter({ has: this.page.getByRole('heading', { name, exact: true }) });
      await expect(card.getByRole('link', { name: `Open ${name}`, exact: true })).toBeVisible();
      await expect(card.getByText(status, { exact: true })).toBeVisible();
    }
  }
  async expectPlaceholder(name) {
    const card = this.cards().filter({ has: this.page.getByRole('heading', { name, exact: true }) });
    await expect(card.getByRole('img', { name: 'No images yet', exact: true })).toBeVisible();
  }
  results() { return this.page.getByRole('list', { name: 'Find a location results example', exact: true }); }
  async expectResult(name, lines, pills) {
    const card = this.results().getByRole('listitem').filter({ has: this.page.getByRole('heading', { name, exact: true }) });
    for (const line of lines) await expect(card.getByText(line, { exact: true })).toBeVisible();
    await expect(card.getByRole('list', { name: 'Suitability', exact: true }).getByRole('listitem')).toHaveText(pills);
  }
  async expectResultColumns(width) {
    const expected = width < 576 ? 1 : width < 768 ? 2 : width < 1200 ? 3 : 4;
    const layout = await this.results().evaluate((element) => ({
      columns: getComputedStyle(element).gridTemplateColumns.split(' ').length,
      width: element.clientWidth,
      scroll: element.scrollWidth,
    }));
    expect(layout.columns).toBe(expected);
    expect(layout.scroll).toBeLessThanOrEqual(layout.width);
  }
  gallery() { return this.page.getByRole('radiogroup', { name: 'Images', exact: true }); }
  async expectGallery(count) {
    await expect(this.gallery().getByRole('radio')).toHaveCount(count);
    await expect(this.gallery().getByRole('radio', { name: 'Image 1, cover', exact: true })).toBeChecked();
    await this.expectStage(1);
  }
  async expectStage(index) {
    await expect(this.page.getByRole('img', { name: `Synthetic location frame ${index}`, exact: true })).toBeVisible();
    await expect(this.page.getByRole('status', { name: 'Selected image', exact: true })).toHaveText(`Image ${index} of 4`);
  }
  async selectImage(index) { await this.gallery().getByRole('radio', { name: new RegExp(`^Image ${index}`) }).check(); }
  async pressArrow(key) { await this.page.keyboard.press(key); }
  async setAsCover() { await this.page.getByRole('button', { name: 'Set as cover', exact: true }).click(); }
  async expectCover(index) {
    await expect(this.gallery().getByRole('radio', { name: `Image ${index}, cover`, exact: true })).toBeVisible();
    await expect(this.page.getByRole('status', { name: 'Gallery feedback', exact: true })).toHaveText(`Image ${index} is now the cover.`);
  }
  report() { return this.page.getByRole('region', { name: 'Scouting report example', exact: true }); }
  async expectReportSections(names) {
    await expect(this.report().getByRole('heading', { level: 3 })).toHaveText(names);
  }
  async expectEntry(section, text, { rating, basis }) {
    const entry = this.report().getByRole('region', { name: section, exact: true }).getByRole('listitem').filter({ hasText: text });
    await expect(entry).toBeVisible();
    if (rating) await expect(entry.getByText(rating, { exact: true })).toBeVisible();
    if (basis) await expect(entry.getByText(basis, { exact: true })).toBeVisible();
  }
  async expectNoScores() {
    await expect(this.report().getByText(/\b\d+(\.\d+)?\s*(\/|%)\s*\d*/)).toHaveCount(0);
  }
  async citeImage(section, text, index) {
    await this.report().getByRole('region', { name: section, exact: true }).getByRole('listitem').filter({ hasText: text })
      .getByRole('button', { name: `Show image ${index}`, exact: true }).click();
    await expect(this.gallery().getByRole('radio', { name: new RegExp(`^Image ${index}`) })).toBeChecked();
    await expect(this.gallery().getByRole('radio', { name: new RegExp(`^Image ${index}`) })).toBeFocused();
  }
  async expectStatusPills(labels) {
    const pills = this.page.getByRole('list', { name: 'Location status pills', exact: true }).getByRole('listitem');
    await expect(pills).toHaveText(labels);
  }
  async retryIndexing() {
    await this.page.getByRole('button', { name: 'Retry', exact: true }).click();
    await expect(this.page.getByRole('status', { name: 'Search index', exact: true })).toHaveText('Updating search');
  }
  async expectModes() {
    const modes = this.page.getByRole('radiogroup', { name: 'Search mode', exact: true });
    await expect(modes.getByRole('radio', { name: 'Keyword', exact: true })).toBeChecked();
    await modes.getByRole('radio', { name: 'Meaning', exact: true }).check();
    await expect(this.page.getByText('Matched by meaning', { exact: true })).toBeVisible();
  }
  async toggleShootType(name) {
    const chip = this.page.getByRole('group', { name: 'Shoot type', exact: true }).getByRole('button', { name, exact: true });
    await chip.click();
    await expect(chip).toHaveAttribute('aria-pressed', 'true');
  }
  async expectFitsViewport() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  async expectNoExternalRequests() {
    expect(this.requests.filter((url) => !url.startsWith('http://127.0.0.1'))).toEqual([]);
  }
  async expectNoAccessibilityViolations() {
    const { default: AxeBuilder } = await import('@axe-core/playwright');
    expect((await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
  }
}
