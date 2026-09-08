import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { ComparePage } from '../page-objects/compare-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';

async function setup(page) {
  const work = new MyWorkPage(page); await work.configureCollection(2);
  work.library.photos.forEach((photo, index) => {
    photo.notes = `Personal notes for attempt ${index + 1}`;
    const critique = savedCritique(index ? 'Demo' : 'Live');
    critique.content.strengths[0].explanation = `Saved strength for attempt ${index + 1}`;
    work.library.critiques.set(photo.id, critique);
  });
  return { library: work.library, compare: new ComparePage(page), signIn: new SignInPage(page) };
}

// Given two owned saved attempts, when their comparison opens, then each complete
// image, current manual context and saved critique is labeled and no analysis runs.
test('L2-005.1: read two complete saved attempts without requesting new analysis', async ({ page }) => {
  const { library, compare, signIn } = await setup(page);
  await signIn.openWithDestination(compare.destination(library.photos[0].id, library.photos[1].id)); await signIn.continue();
  for (let index = 0; index < 2; index++) await compare.expectAttempt(index ? 'Second attempt' : 'First attempt', library.photos[index], library.critiques.get(library.photos[index].id));
  expect(library.calls).toEqual(['compare']);
});

for (const invalid of ['same', 'uncritiqued', 'missing']) test(`L2-005.3: ${invalid} selection has a safe error`, async ({ page }) => {
  const { library, compare, signIn } = await setup(page);
  if (invalid === 'uncritiqued') library.critiques.delete(library.photos[1].id);
  const second = invalid === 'same' ? library.photos[0].id : invalid === 'missing' ? '00000000-0000-4000-8000-999999999999' : library.photos[1].id;
  await signIn.openWithDestination(compare.destination(library.photos[0].id, second)); await signIn.continue();
  if (invalid === 'missing') await compare.expectUnavailable(); else await compare.expectValidation();
});

test('L2-005/L2-030: a failed comparison read can be retried with focus restored', async ({ page }) => {
  const { library, compare, signIn } = await setup(page); library.failures.compare = 1;
  await signIn.openWithDestination(compare.destination(library.photos[0].id, library.photos[1].id)); await signIn.continue();
  await compare.expectFailure(); await compare.retry();
  await compare.expectAttempt('First attempt', library.photos[0], library.critiques.get(library.photos[0].id)); await compare.expectFocus();
});

const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);
for (const viewport of viewports) test(`L2-005.5: full images and labeled attempts fit ${viewport.width}x${viewport.height}`, async ({ page }) => {
  await page.setViewportSize(viewport);
  const { library, compare, signIn } = await setup(page);
  await signIn.openWithDestination(compare.destination(library.photos[0].id, library.photos[1].id)); await signIn.continue();
  await compare.expectAttempt('Second attempt', library.photos[1], library.critiques.get(library.photos[1].id));
  await compare.expectLayout(viewport.width < 768 ? 1 : 2);
});

test('L2-005/L2-043: history navigation after a retry does not steal focus when the next pair loads', async ({ page }) => {
  const { library, compare, signIn } = await setup(page); library.failures.compare = 1;
  await signIn.openWithDestination(compare.destination(library.photos[0].id, library.photos[1].id)); await signIn.continue();
  await compare.expectFailure(); await compare.retry(); await compare.expectFocus();
  await compare.rememberEarlierComparison(library.photos[1].id, library.photos[0].id);
  library.pause('compare'); await page.goBack(); await compare.expectLoading(); await compare.focusLibraryLink();
  library.release('compare');
  await compare.expectAttempt('First attempt', library.photos[1], library.critiques.get(library.photos[1].id));
  await compare.expectLibraryLinkFocus();
});
