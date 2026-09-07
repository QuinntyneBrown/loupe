import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

test('L2-001.3/L2-003.3: absent brief, EXIF and notes remain explicitly absent', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const photo = work.library.photos[0];
  photo.brief = Object.fromEntries(Object.keys(photo.brief).map(key => [key, null]));
  photo.exif = Object.fromEntries(Object.keys(photo.exif).map(key => [key, null]));
  photo.notes = null;
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  await new PhotographDetailPage(page).expectAbsentContext();
});

test('L2-003.3: revisit a saved photograph and its URL without a mutation', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectSaved();
  await detail.expectContentFocus();
  await page.reload();
  await signIn.continue();
  await detail.expectSaved();
  work.library.expectReadsOnly();
});

test('L2-003.4: an unavailable detail has a way back to My Work', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const detail = new PhotographDetailPage(page);
  await detail.openMissing();
  await new SignInPage(page).continue();
  await detail.expectUnavailable();
});

test('L2-003.4: a temporary detail failure offers retry', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  work.library.failures.get = 1;
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectFailure();
  await detail.retry();
  await detail.expectSaved();
});

test('L2-045.1: pagination moves keyboard focus to the first added photograph', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(25);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectPhotographs(24);
  await work.loadMore();
  await work.expectNewPhotographFocus('Study 25');
  await work.openFocusedPhotograph();
  await new PhotographDetailPage(page).expectSaved('Study 25');
});
