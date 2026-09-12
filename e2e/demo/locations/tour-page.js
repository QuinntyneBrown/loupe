import { expect } from '@playwright/test';
import { LocationsPage } from '../../page-objects/locations-page.js';
import { LocationPage } from '../../page-objects/location-page.js';
import { FindLocationPage } from '../../page-objects/find-location-page.js';
import { SignInTourPage } from '../inspiration/tour-page.js';

// Demo-only page objects: the same DOM knowledge as the acceptance page objects,
// plus visible pacing (typed input, image decoding) so the take reads well.
const typing = { delay: 55 };
export { SignInTourPage };

async function decodeVisible(page, selector) {
  const images = page.locator(selector);
  await images.evaluateAll(list => Promise.all(list.filter(i => { const r = i.getBoundingClientRect(); return r.top < innerHeight && r.bottom > 0; }).map(i => i.decode())));
  const loaded = await images.evaluateAll(list => list.filter(i => { const r = i.getBoundingClientRect(); return r.top < innerHeight && r.bottom > 0; }).every(i => i.complete && i.naturalWidth > 0));
  expect(loaded, 'every visible image decoded with real pixels').toBe(true);
}

export class LocationsTourPage extends LocationsPage {
  async ready() {
    await this.expectActiveNavigation();
    await expect(this.page.getByRole('region', { name: 'Your locations', exact: true })).toHaveAttribute('aria-busy', 'false');
    await this.page.evaluate(() => document.fonts.ready);
    await decodeVisible(this.page, 'article img');
  }
  // Focusing <main> after navigation scrolls the heading under the sticky header; scroll back up as a viewer would.
  async top() { await this.page.evaluate(() => window.scrollTo({ top: 0, behavior: 'smooth' })); await expect.poll(() => this.page.evaluate(() => window.scrollY)).toBe(0); }
  async hoverCard(name) { await this.card(name).hover(); }
  async expectFindLink() { await expect(this.page.getByRole('link', { name: 'Find a location', exact: true })).toBeVisible(); }
  async typeFields(values) {
    for (const [label, value] of Object.entries(values)) {
      if (label === 'Setting') await this.field(label).selectOption(value);
      else await this.field(label).pressSequentially(value, typing);
    }
  }
  async typeTag(name) {
    const input = this.dialog().getByRole('textbox', { name: 'Add a tag', exact: true });
    await input.pressSequentially(name, typing); await input.press('Enter');
  }
  async openCard(name) { await this.card(name).getByRole('link', { name: `Open ${name}`, exact: true }).click(); }
}

export class LocationTourPage extends LocationPage {
  async ready(name) {
    await this.expectTitle(name);
    await this.page.evaluate(() => document.fonts.ready);
  }
  async chooseRealFiles(files) {
    await this.imagesDialog().getByLabel('Images', { exact: true }).setInputFiles(files);
  }
  async expectStageDecoded() {
    const stage = this.main().locator('.stage img, figure img').first();
    await expect(stage).toBeVisible();
    await stage.evaluate(i => i.decode());
    expect(await stage.evaluate(i => i.complete && i.naturalWidth > 0), 'the gallery stage decoded with real pixels').toBe(true);
  }
  async appendNotes(field, addition) {
    const box = this.textEditor(field).getByRole('textbox');
    await box.focus(); await box.press('End'); await box.pressSequentially(addition, typing);
  }
  async typeTag(name) {
    const input = this.tags().getByRole('textbox', { name: 'Add a tag', exact: true });
    await input.pressSequentially(name, typing); await input.press('Enter');
  }
  async revealReport() { await this.reportRegion().scrollIntoViewIfNeeded(); }
  async revealNotes() { await this.textEditor('notes').scrollIntoViewIfNeeded(); }
  async revealTags() { await this.tags().scrollIntoViewIfNeeded(); }
  async revealGallery() { await this.gallery().scrollIntoViewIfNeeded(); }
  async expectNotConfigured() {
    await expect(this.reportRegion().getByText(/Integration not configured/)).toBeVisible();
    await expect(this.reportRegion()).toContainText('You can keep editing this location.');
  }
}

export class FindLocationTourPage extends FindLocationPage {
  async openDirect() { await this.page.goto('/locations/find'); await this.expectTitle(); await this.page.evaluate(() => document.fonts.ready); }
  async typeQuery(value) {
    await this.searchbox().fill(''); await this.searchbox().pressSequentially(value, typing); await this.searchbox().press('Enter');
  }
  async decodeResultImages() { await decodeVisible(this.page, 'article img'); }
  // A real query embedding waits on the local model; allow for a cold model rather than the acceptance suite's mocked latency.
  async expectMeaningResults() { await expect(this.results().getByText('Matched by meaning', { exact: true })).toHaveCount(1, { timeout: 30000 }); }
  async expectResultCount(count) { await expect(this.cards()).toHaveCount(count); }
  async firstResultName() { return this.cards().first().getByRole('heading', { level: 2 }).innerText(); }
  async resultNames() { return this.cards().getByRole('heading', { level: 2 }).allInnerTexts(); }
}

export class DesignSystemTourPage {
  constructor(page) { this.page = page; }
  async open(url) { await this.page.goto(url); await expect(this.page.getByRole('heading', { level: 1, name: 'Places, seen before the shoot.', exact: true })).toBeVisible(); await this.page.evaluate(() => document.fonts.ready); }
  report() { return this.page.getByRole('region', { name: 'Scouting report example', exact: true }); }
  async revealReport() { await this.report().scrollIntoViewIfNeeded(); await this.page.evaluate(() => window.scrollBy({ top: -60, behavior: 'smooth' })); }
  async expectSections(names) { await expect(this.report().getByRole('heading', { level: 3 })).toHaveText(names); }
  async cite(section, text, index) {
    await this.report().getByRole('region', { name: section, exact: true }).getByRole('listitem').filter({ hasText: text }).getByRole('button', { name: `Show image ${index}`, exact: true }).click();
    await expect(this.page.getByRole('status', { name: 'Selected image', exact: true })).toHaveText(`Image ${index} of 4`);
  }
  async revealGallery() { await this.page.getByRole('radiogroup', { name: 'Images', exact: true }).scrollIntoViewIfNeeded(); }
}
