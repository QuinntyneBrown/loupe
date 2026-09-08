import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export class ComparePage {
  constructor(page) { this.page = page; }
  destination(firstId, secondId) { return `/compare?firstId=${encodeURIComponent(firstId)}&secondId=${encodeURIComponent(secondId)}`; }
  side(label) { return this.page.getByRole('region', { name: label, exact: true }); }
  async expectAttempt(label, photograph, critique) {
    const side = this.side(label);
    await expect(side.getByRole('heading', { name: photograph.title, exact: true })).toBeVisible();
    await expect(side.getByRole('img', { name: photograph.title, exact: true })).toHaveAttribute('src', photograph.imageUrl);
    await expect(side.locator('time').first()).toHaveAttribute('datetime', photograph.createdAt);
    for (const text of [photograph.brief.intent, photograph.notes, critique.brief.intent, critique.content.strengths[0].explanation])
      await expect(side).toContainText(text);
    for (const heading of ['Strengths', 'Three priorities', 'Practice exercise', 'Exposure', 'Focus', 'Depth of field', 'Motion', 'Lighting', 'Color', 'Processing', 'Framing', 'Subject separation', 'Balance', 'Visual hierarchy', 'Mood'])
      await expect(side.getByRole('heading', { name: heading, exact: true })).toBeVisible();
    await expect(side.getByRole('button', { name: /critique/i })).toHaveCount(0);
  }
  async expectValidation() { await expect(this.page.getByRole('alert')).toHaveText('Choose two different photographs with saved critiques.'); }
  async expectUnavailable() { await expect(this.page.getByRole('alert')).toHaveText('Comparison unavailable. A photograph may have been removed.'); await expect(this.side('First attempt')).toHaveCount(0); }
  async expectFailure() { await expect(this.page.getByRole('alert')).toHaveText('The comparison could not be loaded. Try again.'); }
  async retry() { await this.page.getByRole('button', { name: 'Retry comparison', exact: true }).click(); }
  async expectFocus() { await expect(this.page.getByRole('heading', { name: 'Saved attempts', exact: true })).toBeFocused(); }
  async rememberEarlierComparison(firstId, secondId) {
    await this.page.evaluate(destination => {
      const current = location.href;
      history.replaceState(history.state, '', destination);
      history.pushState(history.state, '', current);
    }, this.destination(firstId, secondId));
  }
  async focusLibraryLink() { await this.page.getByRole('link', { name: 'Back to My Work', exact: true }).focus(); }
  async expectLibraryLinkFocus() { await expect(this.page.getByRole('link', { name: 'Back to My Work', exact: true })).toBeFocused(); }
  async expectLoading() { await expect(this.page.getByRole('status')).toHaveText('Loading comparison…'); }
  async expectLayout(columns) {
    const first = await this.side('First attempt').boundingBox(), second = await this.side('Second attempt').boundingBox();
    if (columns === 2) { expect(Math.abs(first.y - second.y)).toBeLessThan(2); expect(second.x).toBeGreaterThan(first.x + first.width - 2); }
    else expect(second.y).toBeGreaterThan(first.y + first.height - 2);
    for (const label of ['First attempt', 'Second attempt']) {
      const img = this.side(label).getByRole('img');
      const box = await img.boundingBox();
      expect(Math.abs(box.width / box.height - 800 / 600)).toBeLessThan(0.02);
    }
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
}
