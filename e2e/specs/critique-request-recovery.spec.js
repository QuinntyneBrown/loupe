import { test, expect } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

async function open(page) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  return { work, detail: new PhotographDetailPage(page) };
}

// Given rejected stale admission, when latest saved inputs are reviewed, then
// an explicit fresh request uses that revision and unrelated drafts survive.
test('L2-030/L2-043: review the changed saved brief before admitting a fresh request', async ({ page }) => {
  const { work, detail } = await open(page);
  await detail.editNotes('My unfinished observation');
  work.library.photos[0].brief.intent = 'Use motion deliberately';
  work.library.photos[0].revision = 2;
  await detail.requestCritique();
  await detail.expectCritiqueRequestFailure('This photograph changed. Review its latest saved brief before requesting.');
  await detail.expectNoCritiqueRetry();
  await detail.reviewCritiqueRequest();
  await detail.expectLatestCritiqueBrief('Use motion deliberately');
  await detail.expectNotes('My unfinished observation');
  expect(work.library.critiqueOperations.size).toBe(0);
  await detail.requestLatestCritique();
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueRequests.map(request => request.revision)).toEqual([1, 2]);
  expect(work.library.critiqueRequests[1].operationKey).not.toBe(work.library.critiqueRequests[0].operationKey);
  await detail.expectNotes('My unfinished observation');
});

test('L2-030/L2-043: a failed latest-brief read keeps the draft and can be retried', async ({ page }) => {
  const { work, detail } = await open(page);
  await detail.editNotes('Retain this draft');
  work.library.photos[0].revision = 2;
  await detail.requestCritique();
  await detail.expectCritiqueRequestFailure('This photograph changed. Review its latest saved brief before requesting.');
  work.library.failures.get = 1;
  await detail.reviewCritiqueRequest();
  await detail.expectCritiqueRequestFailure('Latest saved details could not be loaded. Your edits are still here.');
  await detail.expectNotes('Retain this draft');
  await detail.reviewCritiqueRequest();
  await detail.expectLatestCritiqueBrief('Explore quiet morning light');
  await detail.requestLatestCritique();
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
});

for (const [code, message] of [
  ['item_unavailable', 'The photograph is unavailable. Your unsaved edits are still here.'],
  ['analysis_active', 'A critique is already active for this photograph. Check its status before requesting again.'],
  ['analysis_limit', 'Too many analyses are active. Wait for one to finish, then retry.'],
]) {
  test(`L2-035/L2-043: ${code} admission explains recovery without discarding notes`, async ({ page }) => {
    const { work, detail } = await open(page);
    await detail.editNotes('Do not lose my notes');
    work.library.errors.requestCritique = [code];
    await detail.requestCritique();
    await detail.expectCritiqueRequestFailure(message);
    await detail.expectNotes('Do not lose my notes');
    if (code !== 'analysis_limit') await detail.expectNoCritiqueRetry();
    else {
      await detail.retryCritiqueRequest();
      await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
    }
  });
}
