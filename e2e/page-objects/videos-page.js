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
    await expect(card.getByRole('link', { name: `Play ${title}`, exact: true })).toHaveAttribute('href', /\/videos\/video-\d+$/);
    const watch = card.getByRole('link', { name: `Open ${title} on youtube.com`, exact: true });
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
    await expect(this.page.getByRole('link', { name: `Play ${title}`, exact: true })).toBeFocused();
  }
  async play(title) {
    await this.card(title).getByRole('link', { name: `Play ${title}`, exact: true }).click();
    await expect(this.page).toHaveURL(/\/videos\/video-\d+$/);
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

export class VideoPlayerPage {
  constructor(page) {
    this.page = page;
  }
  async open(id) {
    await this.page.goto(`/videos/${id}`);
    await new SignInPage(this.page).continue();
  }
  async expectPlayer(title, videoId) {
    const frame = this.page.getByTitle(title, { exact: true });
    await expect(frame).toHaveAttribute('src', `https://www.youtube-nocookie.com/embed/${videoId}?rel=0`);
    await expect(this.page.getByRole('heading', { name: title, exact: true, level: 1 })).toBeVisible();
  }
  async expectDetails({ topic, channel, summary, tags = [], notes }) {
    if (topic) await expect(this.page.getByText(topic, { exact: true })).toBeVisible();
    if (channel) await expect(this.page.getByText(channel, { exact: true })).toBeVisible();
    if (summary) await expect(this.page.getByText(summary, { exact: true })).toBeVisible();
    for (const tag of tags) await expect(this.page.getByText(tag, { exact: true })).toBeVisible();
    if (notes) await expect(this.page.getByText(notes, { exact: true })).toBeVisible();
    else if (notes === null) await expect(this.page.getByText('No notes yet.', { exact: true })).toBeVisible();
  }
  async expectYouTubeLink(url) {
    const link = this.page.getByRole('link', { name: 'Open on YouTube', exact: true });
    await expect(link).toHaveAttribute('href', url);
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    await expect(link).toHaveAttribute('referrerpolicy', 'no-referrer');
  }
  async back() {
    await this.page.getByRole('link', { name: 'Videos', exact: true }).first().click();
    await expect(this.page).toHaveURL(/\/videos$/);
  }
  async edit() {
    await this.page.getByRole('button', { name: 'Edit', exact: true }).click();
  }
  async expectUnavailable() {
    await expect(this.page.getByRole('heading', { name: 'This video is no longer available.', exact: true })).toBeVisible();
    await expect(this.page.getByRole('link', { name: 'Back to Videos', exact: true })).toBeVisible();
  }
  async expectLoadFailure() {
    await expect(this.page.getByRole('heading', { name: "Couldn't load this video.", exact: true })).toBeVisible();
  }
  async retry() {
    await this.page.getByRole('button', { name: 'Try again', exact: true }).click();
  }
  async expectNotice(text) {
    await expect(this.page.getByRole('status')).toContainText(text);
  }
  async expectAccessible() {
    expect((await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  }
}
