import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';

async function setup(page, count) {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(count);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  return { inspiration, detail: new ReferenceDetailPage(page), signIn };
}

// Traces to L2-012, L2-043, L2-044. Mock source/attribution card overlays.
test('reference cards expose attribution and a safe source action on keyboard focus', async ({ page }) => {
  const { inspiration } = await setup(page, 1);
  await inspiration.open();
  await inspiration.expectSourceOverlay('Reference 01', 'Supplied photographer', 'https://source.example/photo');
  expect(inspiration.library.calls).toEqual(['list']);
});

test('reference cards never invent missing attribution, source, or image', async ({ page }) => {
  const { inspiration } = await setup(page, 1);
  Object.assign(inspiration.library.items[0], { attribution: null, sourceUrl: null, previewUrl: null });
  await inspiration.open();
  await inspiration.expectUnknownSource('Reference 01');
});

test('initial load shows skeleton placeholders until the first page arrives', async ({ page }) => {
  const { inspiration } = await setup(page, 4);
  inspiration.library.pause('list');
  await inspiration.open();
  await inspiration.expectLoadingSkeleton(8);
  await inspiration.expectReferences(0);
  inspiration.library.release('list');
  await inspiration.expectLoadingSkeleton(0);
  await inspiration.expectReferences(4);
});

// Given saved inspiration references, when browsed and opened, then complete
// content and supplied source context remain private and distinct from My Work.
test('L2-009.1/L2-012.1/3/4: page references and reopen a full image with a safe source link', async ({ page }) => {
  const { inspiration, detail, signIn } = await setup(page, 25);
  await inspiration.open(); await inspiration.expectOpen(); await inspiration.expectReferences(24);
  await inspiration.loadMore(); await inspiration.expectReferences(25); await inspiration.expectReferenceFocus('Reference 25');
  await inspiration.openReference('Reference 25'); await detail.expectSaved(inspiration.library.items[24]);
  await page.reload(); await signIn.continue(); await detail.expectSaved(inspiration.library.items[24]);
  await detail.openSourceSafely(inspiration.library.items[24].sourceUrl);
  expect(inspiration.library.calls.every(call => ['list', 'get'].includes(call))).toBe(true);
});

test('L2-009.2: unknown source and attribution remain explicitly unknown', async ({ page }) => {
  const { inspiration, detail } = await setup(page, 1);
  const item = inspiration.library.items[0]; item.sourceUrl = null; item.attribution = null; item.notes = null;
  await inspiration.open(); await inspiration.openReference(item.title); await detail.expectSaved(item);
});

test('L2-012.1: a link-only reference has a named placeholder and usable detail', async ({ page }) => {
  const { inspiration, detail } = await setup(page, 1);
  const item = inspiration.library.items[0]; item.previewUrl = null; item.imageUrl = null; item.width = null; item.height = null;
  await inspiration.open(); await inspiration.openReference(item.title); await detail.expectSaved(item);
});

test('L2-012.5: an empty inspiration library is distinct from a failed list', async ({ page }) => {
  const { inspiration } = await setup(page, 0); inspiration.library.failures.list = 1;
  await inspiration.open(); await inspiration.expectFailure(); await inspiration.retry(); await inspiration.expectEmpty();
});

test('L2-012.5/L2-030: a failed later reference page preserves existing choices and recovers focus', async ({ page }) => {
  const { inspiration } = await setup(page, 25); await inspiration.open(); await inspiration.expectReferences(24);
  inspiration.library.failures.list = 1; await inspiration.loadMore(); await inspiration.expectFailure(); await inspiration.expectReferences(24);
  await inspiration.retry(); await inspiration.expectReferences(25); await inspiration.expectReferenceFocus('Reference 25');
});

test('L2-012.5: detail read failure retries while a removed reference has an unavailable state', async ({ page }) => {
  const { inspiration, detail } = await setup(page, 1); await inspiration.open();
  inspiration.library.failures.get = 1; await inspiration.openReference('Reference 01'); await detail.expectFailure();
  await detail.retry(); await detail.expectSaved(inspiration.library.items[0]); await detail.returnToLibrary();
  await inspiration.expectReferences(1); inspiration.library.items = []; await inspiration.openReference('Reference 01'); await detail.expectUnavailable();
});

// Given a failed read, when explicitly retried, then keyboard focus follows the recovered result.
test('L2-030: initial list retry focuses the empty result', async ({ page }) => {
  const { inspiration } = await setup(page, 0); inspiration.library.failures.list = 1;
  await inspiration.open(); await inspiration.expectFailure(); await inspiration.retry();
  await inspiration.expectEmpty(); await inspiration.expectEmptyFocus();
});
test('L2-030: initial list retry focuses the first reference', async ({ page }) => {
  const { inspiration } = await setup(page, 1); inspiration.library.failures.list = 1;
  await inspiration.open(); await inspiration.expectFailure(); await inspiration.retry();
  await inspiration.expectReferences(1); await inspiration.expectReferenceFocus('Reference 01');
});
test('L2-030: detail retry focuses its recovered heading', async ({ page }) => {
  const { inspiration, detail } = await setup(page, 1); await inspiration.open();
  inspiration.library.failures.get = 1; await inspiration.openReference('Reference 01'); await detail.expectFailure();
  await detail.retry(); await detail.expectSaved(inspiration.library.items[0]); await detail.expectHeadingFocus('Reference 01');
});
test('L2-030: browser titles identify the inspiration screens', async ({ page }) => {
  const { inspiration, detail } = await setup(page, 1); await inspiration.open(); await inspiration.expectTitle();
  await inspiration.openReference('Reference 01'); await detail.expectTitle();
});
