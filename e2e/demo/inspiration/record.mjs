// Records one continuous take of the Inspiration library and keyword search
// against the real, isolated demo stack (docs/demo/harness/setup.ps1) after
// seed.mjs has populated the account. `--dry` rehearses every interaction and
// assertion without video or narration pacing.
import { chromium } from 'playwright';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { expect } from '@playwright/test';
import { InspirationTourPage, ReferenceTourPage, SearchTourPage, SignInTourPage } from './tour-page.js';
import { ApiClient } from './api-client.mjs';

const root = fileURLToPath(new URL('../../..', import.meta.url));
const output = path.join(root, 'docs/demo/inspiration/source');
mkdirSync(output, { recursive: true });
const dry = process.argv.includes('--dry');
const scenes = JSON.parse(readFileSync(dry ? new URL('./scenes.json', import.meta.url) : path.join(output, 'narration.json')));
const library = JSON.parse(readFileSync(new URL('./library.json', import.meta.url), 'utf8'));
const images = JSON.parse(readFileSync(new URL('./images.json', import.meta.url), 'utf8'));
const app = process.env.APP_URL || 'https://localhost:4210';
const api = process.env.API_URL || 'https://localhost:5011';
const email = process.env.DEMO_EMAIL || 'photographer@example.com';
const password = process.env.DEMO_PASSWORD || 'local acceptance password';
const live = library.references.find(item => item.seed === library.liveUpload);
const seeded = library.references.filter(item => item.seed !== library.liveUpload);
const find = title => library.references.find(item => item.title === title);
const sourceOf = item => images.find(image => image.seed === item.seed).url;
const newBoard = 'Portrait studies';
const newTag = 'stone';
const noteAddition = ' Come back for the shutters at dusk.';
const errors = []; const unexpectedRequests = []; const apiFailures = []; const timeline = []; const recordings = [];
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
  page.on('response', response => { if (response.url().includes('/api/') && response.status() >= 500) apiFailures.push(`${response.status()} ${response.url()}`); });
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

try {
  const desktop = await createCapture('desktop'); const page = desktop.page;
  const tour = new InspirationTourPage(page); const signIn = new SignInTourPage(page); const detail = new ReferenceTourPage(page); const search = new SearchTourPage(page);
  const lightMatches = library.references.filter(item => /light/i.test([item.title, item.notes, item.attribution, ...item.tags.map(([name]) => name)].join('\n'))).length;
  const architectureLight = seeded.filter(item => item.boards.includes('Architecture') && /light/i.test([item.title, item.notes, item.attribution, ...item.tags.map(([name]) => name)].join('\n'))).map(item => item.title);
  const windowLight = seeded.filter(item => item.tags.some(([name]) => name === 'window light')).length;
  const lindqvist = library.references.filter(item => /lindqvist/i.test(item.attribution + (item.photographer ?? ''))).length;

  await page.goto('/inspiration');
  await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2Finspiration$/);
  await page.evaluate(() => document.fonts.ready);
  await scene(desktop, 'signin', async () => {
    await pause(2500); await signIn.typeCredentials(email, password); await pause(700); await signIn.submit();
    await tour.ready(); await tour.top(); await tour.expectReferences(seeded.length); await tour.expectSummary(`${seeded.length} references on ${library.boards.length} boards`);
  });
  await scene(desktop, 'browse', async () => {
    for (const name of library.boards) await tour.expectBoard(name, seeded.filter(item => item.boards.includes(name)).length);
    await pause(3500);
    const first = find('Coffee, knitted sleeves'); await tour.hoverCard(first.title); await tour.expectOverlay(first.title, first.attribution, sourceOf(first));
    await pause(3500);
    const second = find('Backlit at golden hour'); await tour.hoverCard(second.title); await tour.expectOverlay(second.title, second.attribution, sourceOf(second));
  });
  await scene(desktop, 'save', async () => {
    await pause(800); await tour.openSave(); await expect(tour.dialog()).toBeVisible(); await pause(1500);
    await tour.chooseUpload(); await pause(900);
    await tour.chooseFile(fileURLToPath(new URL(`./images/${live.seed}.jpg`, import.meta.url))); await pause(900);
    await tour.continueDraft(); await tour.expectPreview(); await pause(1200);
    await tour.typeDraft(live.title, live.attribution); await pause(500);
    await tour.tickDraftBoard(live.boards[0]); await pause(900);
    await tour.saveDraft(); await tour.expectSaveClosed();
    await tour.expectReferences(seeded.length + 1); await tour.expectFirstCard(live.title);
    await tour.expectSummary(`${seeded.length + 1} references on ${library.boards.length} boards`);
    await tour.expectBoard(live.boards[0], seeded.filter(item => item.boards.includes(live.boards[0])).length + 1);
    await tour.decodeVisibleImages();
  });
  const portraits = ['Backlit at golden hour', 'Studio sitting no. 4'];
  await scene(desktop, 'boards', async () => {
    await pause(600); await tour.typeNewBoard(newBoard); await tour.expectBoard(newBoard, 0); await pause(1200);
    for (const [index, title] of portraits.entries()) { await tour.pickBoard(title, newBoard); await tour.expectBoard(newBoard, index + 1); await pause(900); }
    await tour.selectBoard(newBoard); await tour.expectBoardTitle(newBoard); await tour.expectReferences(portraits.length); await tour.decodeVisibleImages();
    for (const title of portraits) await expect(tour.card(title)).toBeVisible();
  });
  await scene(desktop, 'tags', async () => {
    await pause(500); await tour.allReferences(); await tour.expectBoardTitle('Inspiration'); await tour.expectReferences(seeded.length + 1); await pause(1800);
    await tour.filterTag('window light'); await tour.expectSelectedTag('window light'); await tour.expectReferences(windowLight); await tour.decodeVisibleImages(); await pause(4500);
    await tour.filterTag('window light'); await tour.expectUnselectedTag('window light'); await tour.expectReferences(seeded.length + 1);
  });
  const opened = find('Doorway, late light');
  await scene(desktop, 'detail', async () => {
    await pause(500); await tour.openReference(opened.title); await detail.expectOpen(opened.title);
    for (const name of opened.boards) await detail.expectBoard(name);
    await pause(3000); await detail.revealTags(); await pause(800);
    await detail.typeTag(newTag); await detail.expectTag(newTag); await detail.expectTagsSaved(); await pause(1500);
    await detail.revealNotes(); await pause(600); await detail.appendNote(noteAddition); await pause(400); await detail.saveNotes(); await detail.expectNotesSaved();
  });
  await scene(desktop, 'search', async () => {
    await pause(400); await search.navigate('Search'); await search.expectNavigation('Search'); await search.expectInitial(); await pause(2200);
    await search.typeQuery('window light'); await search.expectResults(windowLight, windowLight); await search.expectSettled(); await search.decodeResultImages(); await pause(4500);
    await search.typeQuery('Lindqvist'); await search.expectResults(lindqvist + 1, lindqvist + 1); await search.expectSettled();
    await search.expectPhotographerCard('Mara Lindqvist', seeded.filter(item => item.photographer === 'Mara Lindqvist').length);
  });
  await scene(desktop, 'search-filters', async () => {
    await pause(400); await search.typeQuery('light'); await search.expectResults(lightMatches, lightMatches); await search.expectSettled(); await pause(2500);
    await search.filters(); await pause(900); await search.toggleBoard('Architecture'); await search.expectChoice('Architecture', true, 'Boards'); await pause(700); await search.applyFilters();
    await search.expectFilter('Architecture'); await search.expectResults(architectureLight.length, architectureLight.length); await search.expectSettled(); await search.expectTitles(architectureLight); await search.decodeResultImages(); await pause(3200);
    await search.card(opened.title).getByRole('link', { name: opened.title, exact: true }).click({ position: { x: 8, y: 8 } });
    await detail.expectOpen(opened.title); await detail.revealTags(); await detail.expectTag(newTag);
  });
  await scene(desktop, 'close', async () => {
    await page.goto('/inspiration'); await tour.ready(); await page.evaluate(() => document.activeElement?.blur()); await tour.top(); await tour.expectReferences(seeded.length + 1);
    await tour.expectBoard(newBoard, portraits.length); await tour.expectSummary(`${seeded.length + 1} references on ${library.boards.length + 1} boards`);
  });
  expect(errors).toEqual([]); expect(unexpectedRequests).toEqual([]); expect(apiFailures).toEqual([]);
  await closeCapture(desktop);

  // Verify the persisted outcome through the real API, independently of the DOM.
  const client = new ApiClient(api, app); await client.signIn(email, password);
  const collection = await client.get('/api/references?pageSize=24');
  expect(collection.libraryCount).toBe(seeded.length + 1);
  const boards = await client.get('/api/boards');
  const uploaded = await client.get('/api/references/' + collection.items.find(item => item.title === live.title).id);
  expect(uploaded.attribution).toBe(live.attribution);
  expect(uploaded.boardIds).toEqual([boards.find(board => board.name === live.boards[0]).id]);
  expect(boards.find(board => board.name === newBoard).referenceCount).toBe(portraits.length);
  const doorway = await client.get('/api/references/' + collection.items.find(item => item.title === opened.title).id);
  expect(doorway.tags.map(tag => tag.name)).toContain(newTag);
  expect(doorway.notes).toBe(opened.notes + noteAddition);
  const filtered = await client.get('/api/search?query=light&type=all&boardIds=' + boards.find(board => board.name === 'Architecture').id);
  expect(filtered.totalCount).toBe(architectureLight.length);
  const persisted = { references: collection.libraryCount, uploadedReference: uploaded.id, newBoardCount: portraits.length, tagPersisted: true, notesPersisted: true, filteredSearchTotal: filtered.totalCount };
  writeFileSync(path.join(output, dry ? 'preview.json' : 'recording.json'), JSON.stringify({ timeline, recordings, errors, unexpectedRequests, apiFailures, persisted }, null, 2));
  console.log('Verified sign-in, browsing, upload, boards, tag filter, detail edits, keyword search and persisted state:', persisted);
} finally { await browser.close(); }
