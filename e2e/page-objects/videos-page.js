import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { VideoLibrary } from '../fixtures/video-library.js';
import { SignInPage } from './sign-in-page.js';

export class VideosPage {
  constructor(page) {
    this.page = page;
  }
  async configure(count) {
    this.library = new VideoLibrary(count);
    await this.library.attach(this.page);
  }
  async open() {
    await this.page.goto('/videos');
    await new SignInPage(this.page).continue();
  }
  async openFromNavigation() {
    await this.page.getByRole('navigation', { name: 'Library' }).getByRole('link', { name: 'Videos', exact: true }).click();
    await expect(this.page).toHaveURL(/\/videos$/);
  }
  async expectCurrentNavigation() {
    await expect(this.page.getByRole('navigation', { name: 'Library' }).getByRole('link', { name: 'Videos', exact: true })).toHaveAttribute('aria-current', 'page');
    await expect(this.page.getByRole('heading', { name: 'Videos', exact: true })).toBeVisible();
  }
  async expectCount(count) {
    await expect(this.page.getByText(`${count} saved`, { exact: true })).toBeVisible();
  }
  async expectCards(count) {
    await expect(this.page.getByRole('article')).toHaveCount(count);
  }
  card(title) {
    return this.page.getByRole('article').filter({ has: this.page.getByRole('heading', { name: title, exact: true }) });
  }
  async expectCard(title, topic, tags = []) {
    const card = this.card(title);
    const watch = card.getByRole('link', { name: `Watch ${title} on YouTube`, exact: true });
    await expect(watch).toHaveAttribute('href', /^https:\/\/www\.youtube\.com\/watch\?v=/);
    await expect(watch).toHaveAttribute('target', '_blank');
    await expect(watch).toHaveAttribute('rel', 'noopener noreferrer');
    await expect(watch).toHaveAttribute('referrerpolicy', 'no-referrer');
    await expect(card.getByText(topic, { exact: true })).toBeVisible();
    for (const tag of tags) await expect(card.getByText(tag, { exact: true })).toBeVisible();
  }
  async expectIndexing(title) {
    await expect(this.card(title).getByText('Indexing', { exact: true })).toBeVisible();
  }
  async expectNoIndexing(title) {
    await expect(this.card(title).getByText('Indexing', { exact: true })).toHaveCount(0);
  }
  async more() {
    await this.page.getByRole('button', { name: 'Load more', exact: true }).click();
  }
  async retry() {
    await this.page.getByRole('button', { name: 'Try again', exact: true }).click();
  }
  async expectError() {
    await expect(this.page.getByRole('heading', { name: "Couldn't load your videos.", exact: true })).toBeVisible();
  }
  async expectEmpty() {
    await expect(this.page.getByRole('heading', { name: 'Save the videos you learn from.', exact: true })).toBeVisible();
  }
  async expectEmptyFocused() {
    await expect(this.page.getByRole('heading', { name: 'Save the videos you learn from.', exact: true })).toBeFocused();
  }
  async expectCardFocused(title) {
    await expect(this.page.getByRole('link', { name: `Watch ${title} on YouTube`, exact: true })).toBeFocused();
  }
  async search(query) {
    const input = this.page.getByRole('searchbox', { name: 'Search your videos', exact: true });
    await input.fill(query);
    await input.press('Enter');
  }
  async chooseMode(mode) {
    await this.page.getByRole('radiogroup', { name: 'Search mode', exact: true }).getByRole('radio', { name: mode, exact: true }).check();
  }
  async chooseTopic(topic) {
    await this.page.getByRole('radiogroup', { name: 'Topic', exact: true }).getByRole('radio', { name: topic, exact: true }).check();
  }
  async toggleTag(name) {
    await this.page.getByRole('group', { name: 'Tags', exact: true }).getByRole('button', { name: new RegExp(`^${name} `) }).click();
  }
  async expectTagPressed(name, pressed) {
    await expect(this.page.getByRole('group', { name: 'Tags', exact: true }).getByRole('button', { name: new RegExp(`^${name} `) })).toHaveAttribute('aria-pressed', String(pressed));
  }
  async clearFilters() {
    await this.page.getByRole('button', { name: 'Clear filters', exact: true }).click();
  }
  async expectMatching(count) {
    await expect(this.page.getByText(`${count} matching`, { exact: true })).toBeVisible();
  }
  async expectNoMatches() {
    await expect(this.page.getByRole('heading', { name: 'No videos match.', exact: true })).toBeVisible();
  }
  async expectScore(title, text) {
    await expect(this.card(title).getByText(text, { exact: true })).toBeVisible();
  }
  async expectNoScores() {
    await expect(this.page.getByText(/% match$/)).toHaveCount(0);
  }
  async expectQueryRequired() {
    await expect(this.page.getByRole('alert')).toContainText('Describe what you');
  }
  async expectMeaningUnavailable() {
    await expect(this.page.getByRole('alert')).toContainText("Meaning search isn't available right now");
  }
  async useKeyword() {
    await this.page.getByRole('button', { name: 'Use Keyword', exact: true }).click();
  }
  async expectAccessible() {
    expect((await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  }
}

export class VideoDialog {
  constructor(page) {
    this.page = page;
  }
  dialog() {
    return this.page.getByRole('dialog');
  }
  async openAdd() {
    await this.page.getByRole('button', { name: 'Add video', exact: true }).first().click();
    await expect(this.dialog().getByRole('heading', { name: 'Add video', exact: true })).toBeVisible();
  }
  async openEdit(title) {
    await this.page.getByRole('button', { name: `Edit ${title}`, exact: true }).click();
    await expect(this.dialog().getByRole('heading', { name: 'Edit video', exact: true })).toBeVisible();
  }
  async fill({ url, title, topic, channel, summary, notes }) {
    if (url !== undefined) await this.dialog().getByRole('textbox', { name: 'YouTube URL', exact: true }).fill(url);
    if (title !== undefined) await this.dialog().getByRole('textbox', { name: 'Title', exact: true }).fill(title);
    if (topic !== undefined) await this.dialog().getByRole('combobox', { name: 'Topic', exact: true }).selectOption({ label: topic });
    if (channel !== undefined) await this.dialog().getByRole('textbox', { name: /^Channel/ }).fill(channel);
    if (summary !== undefined) await this.dialog().getByRole('textbox', { name: /^What it covers/ }).fill(summary);
    if (notes !== undefined) await this.dialog().getByRole('textbox', { name: /^Your notes/ }).fill(notes);
  }
  async expectValues({ url, title, topic }) {
    if (url !== undefined) await expect(this.dialog().getByRole('textbox', { name: 'YouTube URL', exact: true })).toHaveValue(url);
    if (title !== undefined) await expect(this.dialog().getByRole('textbox', { name: 'Title', exact: true })).toHaveValue(title);
    if (topic !== undefined) await expect(this.dialog().getByRole('combobox', { name: 'Topic', exact: true })).toHaveValue(topic);
  }
  async addTag(name) {
    const input = this.dialog().getByRole('textbox', { name: /^Tags/ });
    await input.fill(name);
    await input.press('Enter');
  }
  async removeTag(name) {
    await this.dialog().getByRole('button', { name: `Remove tag ${name}`, exact: true }).click();
  }
  async expectTags(names) {
    for (const name of names) await expect(this.dialog().getByRole('button', { name: `Remove tag ${name}`, exact: true })).toBeVisible();
  }
  async save() {
    await this.dialog().getByRole('button', { name: 'Save', exact: true }).click();
  }
  async cancel() {
    await this.dialog().getByRole('button', { name: 'Cancel', exact: true }).click();
  }
  async retry() {
    await this.dialog().getByRole('button', { name: 'Try again', exact: true }).click();
  }
  async reviewLatest() {
    await this.dialog().getByRole('button', { name: 'Review latest', exact: true }).click();
  }
  async expectAlert(text) {
    await expect(this.dialog().getByRole('alert')).toContainText(text);
  }
  async expectClosed() {
    await expect(this.dialog()).toHaveCount(0);
  }
  async requestDelete() {
    await this.dialog().getByRole('button', { name: 'Delete', exact: true }).click();
  }
  async confirmDelete(title) {
    const confirm = this.page.getByRole('dialog', { name: `Delete ${title}?`, exact: true });
    await expect(confirm).toBeVisible();
    await confirm.getByRole('button', { name: 'Delete', exact: true }).click();
  }
  async keepEditing() {
    await this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true }).getByRole('button', { name: 'Keep editing', exact: true }).click();
  }
  async discard() {
    await this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true }).getByRole('button', { name: 'Discard', exact: true }).click();
  }
  async expectNotice(text) {
    await expect(this.page.getByRole('status')).toContainText(text);
  }
}
