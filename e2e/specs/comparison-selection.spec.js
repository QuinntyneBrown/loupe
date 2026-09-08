import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { ComparePage } from '../page-objects/compare-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';

async function setup(page, eligible, count = eligible + 1) {
  const work = new MyWorkPage(page); await work.configureCollection(count);
  for (let index = 0; index < eligible; index++) work.library.critiques.set(work.library.photos[index].id, savedCritique());
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  return { work, compare: new ComparePage(page) };
}

// Given a library, when comparison is opened, then select only saved critiqued
// photographs, prevent duplicates, and require two candidates before showing a pair.
test('L2-005.1: choose two distinct eligible attempts from My Work', async ({ page }) => {
  const { work, compare } = await setup(page, 2);
  await work.compareAttempts(); await compare.expectChoosing('first'); await compare.expectChoice('Study 03', false);
  await compare.choose('Study 02'); await compare.expectChoosing('second'); await compare.expectChoice('Study 02', false);
  await compare.choose('Study 01');
  await compare.expectAttempt('First attempt', work.library.photos[1], work.library.critiques.get(work.library.photos[1].id));
  await compare.expectAttempt('Second attempt', work.library.photos[0], work.library.critiques.get(work.library.photos[0].id));
  expect(work.library.calls.every(call => ['list', 'eligible', 'compare'].includes(call))).toBe(true);
});

for (const eligible of [0, 1]) test(`L2-005.2: ${eligible} eligible attempts explains what is needed without empty comparison`, async ({ page }) => {
  const { work, compare } = await setup(page, eligible); await work.compareAttempts(); await compare.expectTooFew();
  expect(work.library.calls).toEqual(['list', 'eligible']);
});

test('L2-005/L2-044: second attempt can be chosen from a later eligible page', async ({ page }) => {
  const { work, compare } = await setup(page, 25); await work.compareAttempts(); await compare.choose('Study 01');
  await compare.expectChoosing('second'); await compare.expectChoice('Study 25', false);
  await compare.loadMoreChoices(); await compare.expectChoiceFocus('Study 25'); await compare.choose('Study 25');
  await compare.expectAttempt('Second attempt', work.library.photos[24], work.library.critiques.get(work.library.photos[24].id));
});

test('L2-005/L2-030: failed candidate reads recover without losing loaded choices', async ({ page }) => {
  const { work, compare } = await setup(page, 25); work.library.failures.eligible = 1;
  await work.compareAttempts(); await compare.expectChoicesFailure(); await compare.retryChoices(); await compare.expectChoice('Study 01');
  work.library.failures.eligible = 1; await compare.loadMoreChoices(); await compare.expectChoicesFailure(); await compare.expectChoice('Study 01');
  await compare.retryChoices(); await compare.expectChoiceFocus('Study 25');
});

test('L2-005/L2-045: photographs with identical titles and dates have distinct accessible choices', async ({ page }) => {
  const { work, compare } = await setup(page, 2);
  work.library.photos[0].title = 'Untitled'; work.library.photos[1].title = 'Untitled';
  await work.compareAttempts(); await compare.chooseNumber('Untitled', 2);
  await compare.expectChoosing('second'); await compare.chooseNumber('Untitled', 1);
  await compare.expectAttempt('First attempt', work.library.photos[1], work.library.critiques.get(work.library.photos[1].id));
});
