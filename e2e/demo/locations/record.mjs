// Records one continuous take of the Locations area — library, images, inline
// editing with live search indexing, the scouting report request, keyword and
// Meaning search — against the real, isolated demo stack
// (docs/demo/harness/setup.ps1 -Ollama) after seed.mjs has populated the
// account. One chapter visits the design-system site for a synthetic report.
// `--dry` rehearses every interaction and assertion without video or pacing.
import { chromium } from 'playwright';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { expect } from '@playwright/test';
import { DesignSystemTourPage, FindLocationTourPage, LocationsTourPage, LocationTourPage, SignInTourPage } from './tour-page.js';
import { ApiClient } from '../inspiration/api-client.mjs';

const root = fileURLToPath(new URL('../../..', import.meta.url));
const output = path.join(root, 'docs/demo/locations/source');
mkdirSync(output, { recursive: true });
const dry = process.argv.includes('--dry');
const scenes = JSON.parse(readFileSync(dry ? new URL('./scenes.json', import.meta.url) : path.join(output, 'narration.json')));
const library = JSON.parse(readFileSync(new URL('./library.json', import.meta.url), 'utf8'));
const app = process.env.APP_URL || 'https://localhost:4212';
const api = process.env.API_URL || 'https://localhost:5013';
const designSystem = process.env.DESIGN_SYSTEM_URL || 'http://127.0.0.1:4187';
const email = process.env.DEMO_EMAIL || 'photographer@example.com';
const password = process.env.DEMO_PASSWORD || 'local acceptance password';
const live = library.locations.find(item => item.key === library.liveAdd);
const seeded = library.locations.filter(item => item.key !== library.liveAdd);
const find = key => library.locations.find(item => item.key === key);
const imagePath = seed => fileURLToPath(new URL(`../inspiration/images/${seed}.jpg`, import.meta.url));
const noteAddition = ' Ask at the boatyard if the gate is shut.';
const newTag = 'mud';
const meaningQuery = 'somewhere calm by the water for two people at first light';
const waterside = ['kew', 'hampstead', 'walthamstow'].map(key => find(key).name);
const errors = []; const unexpectedRequests = []; const apiFailures = []; const expectedRefusals = []; const timeline = []; const recordings = [];
const pause = ms => new Promise(resolve => setTimeout(resolve, dry ? Math.min(ms, 100) : ms));
const browser = await chromium.launch();

async function createCapture(name, width = 1440) {
  const context = await browser.newContext({ viewport: { width, height: 900 }, baseURL: app, ignoreHTTPSErrors: true, recordVideo: dry ? undefined : { dir: output, size: { width, height: 900 } } });
  await context.route('**/*', async route => {
    const url = route.request().url();
    if (/^https?:/.test(url) && !/^https?:\/\/(127\.0\.0\.1|localhost)(:|\/)/.test(url)) { unexpectedRequests.push(url.split('?')[0]); await route.abort(); }
    else await route.fallback();
  });
  await context.addInitScript(() => {
    document.addEventListener('DOMContentLoaded', () => {
      const pointer = document.createElement('div');
      pointer.setAttribute('aria-hidden', 'true');
      pointer.style.cssText = 'position:fixed;width:14px;height:14px;border:2px solid white;border-radius:50%;background:#2563eb99;box-shadow:0 0 0 1px #2563eb;pointer-events:none;z-index:2147483647;display:none';
      document.body.appendChild(pointer);
      document.addEventListener('pointermove', event => { pointer.style.display = 'block'; pointer.style.left = (event.clientX - 7) + 'px'; pointer.style.top = (event.clientY - 7) + 'px'; });
    });
  });
  const page = await context.newPage(); page.setDefaultTimeout(20000);
  page.on('pageerror', error => errors.push(error.message));
  // The one expected 5xx is the scouting request's 503 integration_not_configured: this stack has no Azure OpenAI credentials.
  page.on('response', response => {
    if (!response.url().includes('/api/') || response.status() < 500) return;
    if (response.status() === 503 && /\/scouting-report$/.test(response.url())) expectedRefusals.push(`${response.status()} ${response.url()}`);
    else apiFailures.push(`${response.status()} ${response.url()}`);
  });
  await page.setContent('<body style="margin:0;background:#ff00ff"></body>');
  await pause(2000);
  await page.evaluate(() => document.body.style.background = '#ffffff');
  const anchor = Date.now();
  return { name, context, page, anchor, video: page.video(), width };
}
async function scene(capture, id, action) {
  const specification = scenes.find(s => s.id === id);
  const start = Date.now();
  console.log('Scene:', id);
  await action();
  const duration = specification.duration ?? 12;
  await pause(Math.max(0, duration * 1000 - (Date.now() - start)));
  const end = Date.now();
  await capture.page.screenshot({ path: path.join(output, `${dry ? 'preview' : 'frame'}-${id}.png`) });
  timeline.push({ id, title: specification.title, start: (start - capture.anchor) / 1000, end: (end - capture.anchor) / 1000, capture: capture.name, width: capture.width });
}
async function closeCapture(capture) {
  await capture.context.close();
  if (capture.video) { const filename = path.join(output, capture.name + '.webm'); await capture.video.saveAs(filename); recordings.push({ name: capture.name, filename, width: capture.width }); }
}
const meta = item => `${item.locality} · ${item.images.length} image${item.images.length === 1 ? '' : 's'} · No scouting report`;

let meaningOrder = [];
let current = null;
try {
  const desktop = await createCapture('desktop'); const page = desktop.page; current = page;
  const grid = new LocationsTourPage(page); const signIn = new SignInTourPage(page); const detail = new LocationTourPage(page); const finder = new FindLocationTourPage(page); const design = new DesignSystemTourPage(page);
  const kew = find('kew'); const dunstan = find('dunstan');
  // Results list newest first: the live addition, then the catalogue order the seed created in reverse.
  const newestFirst = [live, ...seeded];
  const arches = newestFirst.filter(item => item.tags.some(([name]) => name === 'arches')).map(item => item.name);
  const river = newestFirst.filter(item => item.tags.some(([name]) => name === 'river')).map(item => item.name);

  await page.goto('/locations');
  await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2Flocations$/);
  await page.evaluate(() => document.fonts.ready);
  await scene(desktop, 'signin', async () => {
    await pause(2500); await signIn.typeCredentials(email, password); await pause(700); await signIn.submit();
    await grid.ready(); await grid.top(); await grid.expectCount(seeded.length); await grid.expectCards(seeded.length);
  });
  await scene(desktop, 'browse', async () => {
    await grid.expectFindLink(); await pause(2500);
    for (const key of ['kew', 'hampstead']) {
      const item = find(key); await grid.hoverCard(item.name); await grid.expectCard(item.name, { cover: true, meta: meta(item) }); await pause(3200);
    }
    await grid.hoverCard(find('studio').name); await grid.expectCard(find('studio').name, { cover: true, meta: meta(find('studio')) });
  });
  await scene(desktop, 'add', async () => {
    await pause(600); await grid.openAdd(); await pause(900);
    await grid.typeFields({ Name: live.name, Locality: live.locality, Region: live.region, Country: live.country, Setting: live.setting, 'Scouting brief': live.scoutingBrief, Notes: live.notes });
    for (const [name] of live.tags) await grid.typeTag(name);
    await pause(700); await grid.save(); await grid.expectClosed();
    await grid.expectNotice(`“${live.name}” saved.`); await grid.expectCount(seeded.length + 1);
    await grid.expectOrder([live.name, ...seeded.map(item => item.name)]);
    await grid.expectCard(live.name, { cover: false, meta: `${live.locality} · No images · No scouting report` });
  });
  await scene(desktop, 'images', async () => {
    await pause(500); await grid.openCard(live.name); await detail.ready(live.name); await detail.expectNoImages(); await pause(1200);
    await detail.openAddImages(); await pause(800);
    await detail.chooseRealFiles(live.images.map(imagePath)); await pause(900);
    await detail.startUpload(); await detail.finishUploads();
    await detail.expectGalleryCount(live.images.length); await detail.expectSelected(1, true); await detail.expectStageUncropped(live.name, 1); await pause(1600);
    await detail.focusThumbnail(1); await detail.pressKey('ArrowRight'); await detail.expectSelected(2, false); await pause(900);
    await detail.pressKey('ArrowRight'); await detail.expectSelected(3, false); await pause(900);
    await detail.setAsCover(); await detail.expectSelected(3, true);
  });
  await scene(desktop, 'detail', async () => {
    await pause(400); await detail.revealNotes(); await pause(800);
    await detail.appendNotes('notes', noteAddition); await pause(400); await detail.saveText('notes'); await detail.expectTextStatus('notes', 'Saved');
    await detail.expectIndexStatus('Updating search'); await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'smooth' }));
    await expect(detail.indexStatus()).toHaveCount(0, { timeout: 30000 }); await pause(1500);
    await detail.revealTags(); await pause(500); await detail.typeTag(newTag); await detail.saveTags(); await detail.expectTagStatus('Saved');
    await detail.expectTags([...live.tags.map(([name]) => name), newTag].sort((a, b) => a.localeCompare(b)));
    await expect(detail.indexStatus()).toHaveCount(0, { timeout: 30000 });
  });
  await scene(desktop, 'report', async () => {
    await pause(400); await detail.revealReport(); await detail.expectRequestAvailable(); await detail.expectDisclosure(); await pause(4500);
    await detail.requestReport(); await detail.expectNotConfigured(); await detail.expectNotesEditable();
  });
  await scene(desktop, 'report-example', async () => {
    await design.open(designSystem + '/locations.html'); await pause(600); await design.revealReport(); await pause(1500);
    await design.expectSections(['Overview', 'Suitability', 'Time of day', 'Techniques', 'Group size', 'Cautions']); await pause(4500);
    await design.cite('Overview', 'Repeating arches give depth', 3); await design.revealGallery();
  });
  await scene(desktop, 'find-keyword', async () => {
    await finder.openDirect(); await finder.expectInitial(); await pause(2200);
    await finder.typeQuery('arches'); await finder.expectResultsHead(`${arches.length} locations for “arches”`); await finder.expectCards(arches); await finder.decodeResultImages(); await pause(2500);
    await finder.chooseSetting('Outdoor'); await finder.expectCards([kew.name]); await pause(2200);
    await finder.chooseSetting('Any'); await finder.expectCards(arches); await pause(800);
    await finder.typeQuery(''); await finder.expectResultsHead(`${seeded.length + 1} locations`); await pause(1200);
    await finder.openFilters('Tags'); await pause(1200); await finder.toggleDialogChip('Tags', 'river'); await pause(700); await finder.applyFilters();
    await finder.expectCards(river); await finder.expectCard(live.name, { place: `${live.locality} · ${live.images.length} images`, noReport: true }); await finder.decodeResultImages();
  });
  await scene(desktop, 'find-meaning', async () => {
    await pause(400); await finder.clearAll(); await finder.expectResultsHead(`${seeded.length + 1} locations`); await pause(600);
    await finder.chooseMode('Meaning'); await finder.expectQueryRequired(); await pause(1200);
    await finder.typeQuery(meaningQuery); await finder.expectMeaningResults(); await finder.decodeResultImages();
    meaningOrder = await finder.resultNames();
    expect(meaningOrder.length).toBeGreaterThanOrEqual(1);
    expect(waterside, 'the top Meaning result is a waterside location').toContain(meaningOrder[0]);
    expect(meaningOrder[0]).not.toBe(find('studio').name);
    await expect(page).toHaveURL(/mode=meaning/);
  });
  await scene(desktop, 'close', async () => {
    await page.goto('/locations'); await grid.ready(); await page.evaluate(() => document.activeElement?.blur()); await grid.top();
    await grid.expectCount(seeded.length + 1); await grid.expectCard(live.name, { cover: true, meta: `${live.locality} · ${live.images.length} images · No scouting report` });
  });
  expect(errors).toEqual([]); expect(unexpectedRequests).toEqual([]); expect(apiFailures).toEqual([]); expect(expectedRefusals).toHaveLength(1);
  await closeCapture(desktop);

  // Verify the persisted outcome through the real API, independently of the DOM.
  const client = new ApiClient(api, app); await client.signIn(email, password);
  const collection = await client.get('/api/locations');
  expect(collection.totalCount).toBe(seeded.length + 1);
  const added = await client.get('/api/locations/' + collection.items.find(item => item.name === live.name).id);
  expect(added.images.length).toBe(live.images.length);
  expect(added.coverImageId).toBe(added.images[2].id);
  expect(added.notes).toBe(live.notes + noteAddition);
  expect(added.tags.map(tag => tag.name)).toContain(newTag);
  expect(added.indexStatus).toBe('current');
  expect(added.reportStatus).toBe('None');
  const tagged = await client.get('/api/locations/search?query=&tags=river');
  expect(tagged.totalCount).toBe(river.length);
  const meaning = await client.get('/api/locations/search?mode=meaning&query=' + encodeURIComponent(meaningQuery));
  expect(meaning.items.map(item => item.name)).toEqual(meaningOrder);
  const persisted = { locations: collection.totalCount, addedLocation: added.id, images: added.images.length, coverIsThird: added.coverImageId === added.images[2].id, notesPersisted: true, tagPersisted: true, indexStatus: added.indexStatus, reportStatus: added.reportStatus, taggedSearchTotal: tagged.totalCount, meaningOrder };
  writeFileSync(path.join(output, dry ? 'preview.json' : 'recording.json'), JSON.stringify({ timeline, recordings, errors, unexpectedRequests, apiFailures, expectedRefusals, persisted }, null, 2));
  console.log('Verified sign-in, browsing, add, images, inline edits with live indexing, report request, keyword and meaning search, and persisted state:', persisted);
} catch (error) {
  if (current) await current.screenshot({ path: path.join(output, 'failure.png') }).catch(() => {});
  throw error;
} finally { await browser.close(); }
