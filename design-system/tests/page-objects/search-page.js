import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export class SearchPage {
  constructor(page) {
    this.page = page;
    this.query = page.getByRole('searchbox', { name: 'Search your library' });
    this.results = page.getByRole('region', { name: 'Search results', exact: true });
    this.filters = page.getByRole('dialog', { name: 'Boards and tags', exact: true });
    this.requests = [];
    page.on('request', request => this.requests.push(request.url()));
  }

  async open() { await this.page.goto('/search.html'); }
  async expectInitial() {
    await expect(this.page.getByRole('heading', { name: "Describe what you're looking for." })).toBeVisible();
    await expect(this.page.getByText('Keyword matches titles, tags, and notes. Meaning search is not available in this example.')).toBeVisible();
    await expect(this.results.getByRole('article')).toHaveCount(0);
  }
  async search(query) { await this.query.fill(query); await this.query.press('Enter'); }
  async selectType(type) { await this.page.getByRole('radio', { name: type, exact: true }).check(); }
  async useExample(query) { await this.page.getByRole('button', { name: `Try “${query}”`, exact: true }).click(); }
  async expectQuery(query) { await expect(this.query).toHaveValue(query); }
  async expectResults(names) {
    await expect(this.results.getByRole('article')).toHaveCount(names.length);
    for (const name of names) await expect(this.results.getByRole('article', { name, exact: true })).toBeVisible();
  }
  async expectStatus(text) { await expect(this.page.getByRole('status', { name: 'Search feedback' })).toContainText(text); }
  async expectEmpty(query) {
    await expect(this.results.getByRole('heading', { name: query ? `Nothing matched “${query}”.` : 'Nothing matched your filters.', exact: true })).toBeVisible();
    await expect(this.results.getByRole('button', { name: 'Edit query' })).toBeVisible();
    await expect(this.results.getByRole('button', { name: 'Try again', exact: true })).toHaveCount(0);
  }
  async editQuery() {
    await this.results.getByRole('button', { name: 'Edit query' }).click();
    await expect(this.query).toBeFocused();
  }
  async expectSafeSources() {
    const sources = this.results.getByRole('link');
    expect(await sources.count()).toBeGreaterThan(0);
    for (const link of await sources.all()) {
      await expect(link).toHaveAttribute('href', /^https:\/\/example\.com\//);
      await expect(link).toHaveAttribute('target', '_blank');
      await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
      await expect(link).toHaveAccessibleName(/^Open (source|website) for /);
    }
  }
  async expectNoPreview(name) {
    await expect(this.results.getByRole('article', { name, exact: true }).getByText('No preview available', { exact: true })).toBeVisible();
  }
  async openResult(name) { await this.results.getByRole('button', { name: `Open ${name}`, exact: true }).click(); }
  async expectDetail(name) {
    const detail = this.page.getByRole('dialog', { name, exact: true });
    await expect(detail).toBeVisible();
    await expect(detail).toContainText('Synthetic saved item');
    await detail.getByRole('button', { name: 'Close example' }).click();
    await expect(this.results.getByRole('button', { name: `Open ${name}`, exact: true })).toBeFocused();
  }
  async chooseState(state) { await this.page.getByLabel('Example state', { exact: true }).selectOption({ label: state }); }
  async expectLoading() {
    await expect(this.results).toHaveAttribute('aria-busy', 'true');
    await this.expectStatus('Searching');
    await expect(this.results.getByRole('article')).toHaveCount(0);
  }
  async completeLoading() { await this.page.getByRole('button', { name: 'Complete loading', exact: true }).click(); }
  async expectFailure() {
    await expect(this.results.getByRole('alert')).toContainText("Search isn't available right now.");
    await expect(this.results.getByRole('heading', { name: /Nothing matched/ })).toHaveCount(0);
    await this.expectQuery('window');
  }
  async retry() { await this.results.getByRole('button', { name: 'Try again', exact: true }).click(); }
  async loadMore() { await this.results.getByRole('button', { name: 'Load more', exact: true }).click(); }
  async typeQuery(query) { await this.query.fill(query); }
  async expectQueryFocused() { await expect(this.query).toBeFocused(); }
  async expectPageFailure() {
    await expect(this.results.getByRole('alert')).toContainText("More results couldn't be loaded.");
    await expect(this.results.getByRole('article')).toHaveCount(4);
  }
  async retryPage() { await this.results.getByRole('button', { name: 'Retry more results', exact: true }).click(); }
  async expectResultFocused(name) { await expect(this.results.getByRole('button', { name: `Open ${name}`, exact: true })).toBeFocused(); }
  async openFilters() { await this.page.getByRole('button', { name: 'Boards and tags', exact: true }).click(); }
  async toggleFilter(group, name) { await this.filters.getByRole('group', { name: group, exact: true }).getByRole('button', { name, exact: true }).click(); }
  async expectSelection(group, name, selected) {
    await expect(this.filters.getByRole('group', { name: group, exact: true }).getByRole('button', { name, exact: true })).toHaveAttribute('aria-pressed', String(selected));
  }
  async applyFilters() { await this.filters.getByRole('button', { name: 'Apply', exact: true }).click(); }
  async cancelFilters() { await this.filters.getByRole('button', { name: 'Cancel', exact: true }).click(); }
  async escapeFilters() { await this.page.keyboard.press('Escape'); }
  async clearDraft() { await this.filters.getByRole('button', { name: 'Clear', exact: true }).click(); }
  async clearFilters() { await this.page.getByRole('button', { name: 'Clear all filters', exact: true }).click(); }
  async removeFilter(group, name) { await this.page.getByRole('button', { name: `Remove ${group} filter ${name}`, exact: true }).click(); }
  async expectActiveFilter(group, name, active = true) {
    await expect(this.page.getByRole('button', { name: `Remove ${group} filter ${name}`, exact: true })).toHaveCount(active ? 1 : 0);
  }
  async expectFilterReturnFocus() {
    await expect(this.filters).not.toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Boards and tags', exact: true })).toBeFocused();
  }
  async expectBoardExplanation() {
    await expect(this.results.getByText('Boards hold references only. Remove the board filter or choose All or References.', { exact: true })).toBeVisible();
  }
  async expectUnavailableBoard() {
    await expect(this.results.getByText('A selected board is unavailable. Remove it explicitly to search the remaining library.', { exact: true })).toBeVisible();
    await this.expectActiveFilter('board', 'Unavailable board');
  }
  async expectFitsViewport() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  async expectMixedLayout(width) {
    const grid = this.results.locator('.search-grid');
    const available = Math.min(1280, width) - (width < 640 ? 32 : 48);
    const columns = width < 640 ? 2 : Math.floor((available + 16) / 236);
    await expect.poll(() => grid.evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length)).toBe(columns);
    const reference = await this.results.getByRole('article', { name: 'Morning by the window', exact: true }).boundingBox();
    const photographer = await this.results.getByRole('article', { name: 'Mara Lindqvist', exact: true }).boundingBox();
    expect(photographer.width).toBeGreaterThan(reference.width * 1.9);
    expect(Math.abs(reference.height / reference.width - 1.25)).toBeLessThan(0.03);
  }
  async expectReferenceFirstRow() {
    const first = await this.results.getByRole('article', { name: 'Morning by the window', exact: true }).boundingBox();
    const second = await this.results.getByRole('article', { name: 'Window study without a preview', exact: true }).boundingBox();
    const photographer = await this.results.getByRole('article', { name: 'Mara Lindqvist', exact: true }).boundingBox();
    expect(second.y).toBe(first.y);
    expect(photographer.y).toBeGreaterThan(first.y + first.height);
  }
  async expectDialogUsable() {
    await expect(this.filters).toBeVisible();
    const box = await this.filters.boundingBox();
    const viewport = this.page.viewportSize();
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width + 1);
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height + 1);
    for (const name of ['Apply', 'Cancel', 'Clear']) {
      const button = this.filters.getByRole('button', { name, exact: true });
      await expect(button).toBeInViewport({ ratio: 1 });
      const bounds = await button.boundingBox();
      expect(bounds.y + bounds.height).toBeLessThanOrEqual(box.y + box.height);
    }
    expect(await this.filters.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
  }
  async expectKeyboardDialog() {
    await expect(this.filters.getByRole('button', { name: 'Window light', exact: true })).toBeFocused();
    await this.page.keyboard.press('Space');
    await this.expectSelection('Boards', 'Window light', true);
    for (let index = 0; index < 24; index++) {
      await this.page.keyboard.press(index < 12 ? 'Tab' : 'Shift+Tab');
      expect(await this.filters.evaluate(node => node.contains(document.activeElement))).toBe(true);
    }
    const focused = this.page.locator(':focus');
    const outline = await focused.evaluate(node => ({ style: getComputedStyle(node).outlineStyle, width: parseFloat(getComputedStyle(node).outlineWidth) }));
    expect(outline.style).not.toBe('none');
    expect(outline.width).toBeGreaterThanOrEqual(2);
  }
  async expectDialogBoundaryFocus() {
    const first = this.filters.getByRole('button', { name: 'Close filters', exact: true });
    const last = this.filters.getByRole('button', { name: 'Apply', exact: true });
    await last.focus();
    await this.page.keyboard.press('Tab');
    await expect(first).toBeFocused();
    await this.page.keyboard.press('Shift+Tab');
    await expect(last).toBeFocused();
  }
  async expectTagLimit() {
    await expect(this.filters.getByRole('alert')).toHaveText('Choose up to 10 tags.');
  }
  async expectTargetsUsable() {
    for (const control of await this.page.locator('main button:visible, main a:visible, .search-types label:visible').all()) {
      const box = await control.boundingBox();
      expect(box.height, await control.innerText()).toBeGreaterThanOrEqual(40);
      expect(box.width, await control.innerText()).toBeGreaterThanOrEqual(24);
    }
  }
  async useTextSpacing() {
    await this.page.addStyleTag({ content: '* { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; } p { margin-bottom: 2em !important; }' });
  }
  async expectReducedMotion() {
    const duration = await this.results.locator('.search-skeleton').first().evaluate(node => getComputedStyle(node).animationDuration);
    expect(duration).toBe('0s');
  }
  async screenshot(path, fullPage = true) { await this.page.screenshot({ path, fullPage }); }
  async expectOnlyLocalRequests() {
    const origin = new URL(this.page.url()).origin;
    expect(this.requests.filter(url => new URL(url).origin !== origin)).toEqual([]);
  }
  async expectNoAccessibilityViolations() {
    expect((await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
  }
}
