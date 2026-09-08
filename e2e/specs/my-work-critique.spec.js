import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { critiqueOperation } from '../fixtures/critique-operation.js';
import { savedCritique } from '../fixtures/saved-critique.js';

const states = [
  [null, false, 'No critique yet'], ['Queued', false, 'Critique queued'],
  ['Running', false, 'Critique running'], ['Succeeded', true, 'Critique ready'],
  ['Failed', false, 'Critique failed'], ['Failed', true, 'Critique failed · Previous critique available'],
  ['Queued', true, 'Critique queued · Previous critique available'],
  ['Running', true, 'Critique running · Previous critique available'],
  ['Canceled', false, 'Critique canceled'], ['Canceled', true, 'Critique canceled · Previous critique available'],
];

// Given mixed critique states, when My Work is opened, then each card describes
// the current job separately from saved-result availability using the list response.
test('L2-003.2: library cards distinguish current jobs and available critiques without per-card reads', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(states.length);
  states.forEach(([status, saved], index) => {
    const id = work.library.photos[index].id;
    if (status) work.library.critiqueOperations.set(id, critiqueOperation(id, status));
    if (saved) work.library.critiques.set(id, savedCritique());
  });
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  for (let index = 0; index < states.length; index++)
    await work.expectCritiqueLabel(work.library.photos[index].title, states[index][2]);
  expect(work.library.calls).toEqual(['list']);
});

test('L2-003.2: returning to My Work refreshes a failed replacement while keeping its earlier result', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(1);
  const photo = work.library.photos[0];
  const operation = critiqueOperation(photo.id, 'Queued');
  work.library.critiqueOperations.set(photo.id, operation); work.library.critiques.set(photo.id, savedCritique());
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await work.expectCritiqueLabel(photo.title, 'Critique queued · Previous critique available');
  await work.openPhotograph(photo.title);
  const detail = new PhotographDetailPage(page); await detail.expectCritique();
  operation.status = 'Failed'; operation.failureCode = 'provider_timeout'; operation.message = 'Analysis failed.';
  await detail.returnToLibrary(); await work.expectCritiqueLabel(photo.title, 'Critique failed · Previous critique available');
  await work.openPhotograph(photo.title); await detail.expectCritique(); await detail.expectCritiqueStatus('Failed', 'Analysis failed.');
});

test('L2-003.1/2: critique state remains attached to the correct card on later pages', async ({ page }) => {
  const work = new MyWorkPage(page); await work.configureCollection(25);
  const last = work.library.photos[24]; work.library.critiqueOperations.set(last.id, critiqueOperation(last.id, 'Running'));
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await work.expectPhotographs(24); await work.loadMore(); await work.expectPhotographs(25);
  await work.expectCritiqueLabel(last.title, 'Critique running'); await work.expectEnd();
  expect(work.library.calls).toEqual(['list', 'list']);
});
