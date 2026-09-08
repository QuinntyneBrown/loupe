import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';

test('L2-003.1: browse all photographs through explicit pagination', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(25);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectPhotographs(24);
  await work.loadMore();
  await work.expectPhotographs(25);
  await work.expectEnd();
});

test('initial load shows skeleton placeholders until the first page arrives', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(4);
  work.library.pause('list');
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectLoadingSkeleton(8);
  await work.expectPhotographs(0);
  work.library.release('list');
  await work.expectLoadingSkeleton(0);
  await work.expectPhotographs(4);
});

test('L2-003.4: an empty successful collection has an honest empty state', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectEmpty();
  await work.expectPhotographs(0);
});

test('L2-003.4: failed initial load offers retry without claiming an empty collection', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(25, 1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectFailure();
  await work.retry();
  await work.expectPhotographs(24);
});

test('L2-003.4: a failed next page preserves existing photographs and retries the same page', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(25);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectPhotographs(24);
  await work.failNextPage();
  await work.loadMore();
  await work.expectFailure();
  await work.expectPhotographs(24);
  await work.retry();
  await work.expectPhotographs(25);
  await work.expectEnd();
});
