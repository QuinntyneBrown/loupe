import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { ComparePage } from '../page-objects/compare-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';

async function setup(page, count = 3) {
  const work = new MyWorkPage(page); await work.configureCollection(count);
  work.library.photos.forEach(photo => work.library.critiques.set(photo.id, savedCritique()));
  const photos = [...work.library.photos], compare = new ComparePage(page), signIn = new SignInPage(page);
  await signIn.openWithDestination(compare.destination(photos[0].id, photos[1].id)); await signIn.continue();
  await compare.expectAttempt('First attempt', photos[0], work.library.critiques.get(photos[0].id));
  return { library: work.library, photos, compare };
}

// Given a displayed pair and a later deletion, when refreshed, then keep the
// surviving saved attempt and replace the missing side without starting analysis.
for (const removed of [0, 1]) test(`L2-005.4: removing ${removed ? 'second' : 'first'} attempt preserves and replaces the missing side`, async ({ page }) => {
  const { library, photos, compare } = await setup(page);
  const label = removed ? 'Second attempt' : 'First attempt', survivor = removed ? 'First attempt' : 'Second attempt';
  library.photos.splice(removed, 1); library.pause('compare'); await compare.refresh(); await compare.expectLoading();
  await compare.expectAttempt(survivor, photos[1 - removed], library.critiques.get(photos[1 - removed].id));
  library.release('compare'); await compare.expectMissing(label);
  await compare.replace(label); await compare.expectReplacementDialog(label);
  await compare.expectChoice(photos[1 - removed].title, false); await compare.choose(photos[2].title);
  await compare.expectAttempt(label, photos[2], library.critiques.get(photos[2].id));
  await compare.expectAttempt(survivor, photos[1 - removed], library.critiques.get(photos[1 - removed].id));
  expect(library.calls.every(call => ['compare', 'get', 'getCritique', 'eligible'].includes(call))).toBe(true);
});

test('L2-005.4/L2-045: cancel replacement and retain focus and the surviving attempt', async ({ page }) => {
  const { library, photos, compare } = await setup(page); library.photos.shift();
  await compare.refresh(); await compare.expectMissing('First attempt'); await compare.replace('First attempt');
  await compare.expectReplacementDialog('First attempt'); await compare.cancelReplacement(); await compare.expectReplacementFocus('First attempt');
  await compare.expectAttempt('Second attempt', photos[1], library.critiques.get(photos[1].id));
});

test('L2-005.4/L2-030: a transient refresh failure retains both saved attempts and retries', async ({ page }) => {
  const { library, photos, compare } = await setup(page); library.failures.compare = 1;
  await compare.refresh(); await compare.expectFailure();
  await compare.expectAttempt('First attempt', photos[0], library.critiques.get(photos[0].id));
  photos[1].notes = 'Later saved notes'; await compare.retry();
  await compare.expectAttempt('Second attempt', photos[1], library.critiques.get(photos[1].id));
});

test('L2-005.4/L2-030: failed independent survivor refresh retains its previous content with an honest warning', async ({ page }) => {
  const { library, photos, compare } = await setup(page); library.photos.shift();
  library.errors.get = ['item_unavailable', 'request_failed'];
  await compare.refresh(); await compare.expectMissing('First attempt'); await compare.expectRecoveryFailure();
  await compare.expectAttempt('Second attempt', photos[1], library.critiques.get(photos[1].id));
  await compare.retry(); await compare.expectMissing('First attempt');
  await compare.expectAttempt('Second attempt', photos[1], library.critiques.get(photos[1].id));
});

test('L2-005.4: both deleted attempts can be replaced one at a time', async ({ page }) => {
  const { library, photos, compare } = await setup(page, 4); library.photos.splice(0, 2);
  await compare.refresh(); await compare.expectMissing('First attempt'); await compare.expectMissing('Second attempt');
  await compare.replace('First attempt'); await compare.choose(photos[2].title);
  await compare.expectAttempt('First attempt', photos[2], library.critiques.get(photos[2].id)); await compare.expectMissing('Second attempt');
  await compare.replace('Second attempt'); await compare.choose(photos[3].title);
  await compare.expectAttempt('Second attempt', photos[3], library.critiques.get(photos[3].id));
});

test('L2-005.4/L2-043: history navigation closes replacement selection for the old pair', async ({ page }) => {
  const { library, photos, compare } = await setup(page, 4); library.photos.shift();
  await compare.refresh(); await compare.expectMissing('First attempt'); await compare.replace('First attempt');
  await compare.expectReplacementDialog('First attempt');
  await compare.rememberEarlierComparison(photos[2].id, photos[3].id); await page.goBack();
  await compare.expectNoReplacement();
  await compare.expectAttempt('First attempt', photos[2], library.critiques.get(photos[2].id));
  await compare.expectAttempt('Second attempt', photos[3], library.critiques.get(photos[3].id));
});
