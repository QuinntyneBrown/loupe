import { test, expect } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { critiqueOperation } from '../fixtures/critique-operation.js';
import { savedCritique } from '../fixtures/saved-critique.js';

async function open(page, status = 'Queued', previous = false, failedRead = false) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const id = work.library.photos[0].id;
  const operation = critiqueOperation(id, status);
  work.library.critiqueOperations.set(id, operation);
  if (previous) work.library.critiques.set(id, savedCritique());
  if (failedRead) work.library.failures.getCritique = 1;
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  return { work, operation, id, detail: new PhotographDetailPage(page) };
}

// Given a durable operation, when the state changes or the screen is revisited,
// then status and saved results update while editing and earlier content survive.
test('L2-033.2/L2-034.1: follow queued, running and successful critique without reloading or moving editor focus', async ({ page }) => {
  await page.clock.install();
  const { work, operation, id, detail } = await open(page, 'Queued', false, true);
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  await detail.expectCritiqueFailure();
  await detail.retryCritique();
  await detail.expectNoCritique();
  await detail.editNotes('An unfinished personal observation');
  await detail.focusNotes();
  operation.status = 'Running'; operation.message = 'Analyzing the photograph.';
  await page.clock.fastForward(5000);
  await detail.expectCritiqueStatus('Running', operation.message);
  await detail.expectNotesFocus();
  operation.status = 'Succeeded'; operation.message = 'Critique saved.'; operation.completedAt = '2026-09-07T13:01:00Z';
  work.library.critiques.set(id, { ...savedCritique(), operationId: operation.id });
  await page.clock.fastForward(5000);
  await detail.expectCritiqueStatus('Succeeded', 'Critique saved.');
  await detail.expectCritique();
  await detail.expectNotes('An unfinished personal observation');
  await detail.expectNotesFocus();
  work.library.expectReadsOnly();
});

test('L2-033.2/L2-034.5: revisit a waiting replacement with the earlier critique still readable', async ({ page }) => {
  const { work, operation, detail } = await open(page, 'Queued', true);
  operation.nextAttemptAt = '2026-09-07T13:05:00Z'; operation.message = 'Waiting before trying again.';
  await detail.checkCritiqueStatus();
  await detail.expectCritiqueRetryTime(operation.nextAttemptAt);
  await detail.expectCritique();
  await detail.returnToLibrary();
  await work.openPhotograph('Study 01');
  await detail.expectCritiqueStatus('Queued', operation.message);
  await detail.expectCritique();
  work.library.expectReadsOnly();
});

for (const status of ['Failed', 'Canceled']) {
  test(`L2-033.2/L2-034.3: ${status} replacement preserves the earlier result and stops polling`, async ({ page }) => {
    await page.clock.install();
    const { work, operation, detail } = await open(page, 'Running', true);
    await detail.expectCritiqueStatus('Running', operation.message);
    operation.status = status; operation.message = status === 'Failed' ? 'The service is unavailable. Try again later.' : 'The operation was canceled.';
    operation.completedAt = '2026-09-07T13:01:00Z';
    await page.clock.fastForward(5000);
    await detail.expectCritiqueStatus(status, operation.message);
    await detail.expectCritique();
    const before = work.library.calls.length;
    await page.clock.fastForward(15000);
    expect(work.library.calls).toHaveLength(before);
    work.library.expectReadsOnly();
  });
}

test('L2-033.2/L2-043: failed status refresh preserves known state and recovers without overlapping reads', async ({ page }) => {
  await page.clock.install();
  const { work, operation, detail } = await open(page, 'Running', true);
  await detail.expectCritiqueStatus('Running', operation.message);
  work.library.failures.getCritiqueOperation = 1;
  await detail.checkCritiqueStatus();
  await detail.expectCritiqueStatusFailure();
  await detail.expectCritiqueStatus('Running', operation.message);
  await detail.expectCritique();
  work.library.pause('getCritiqueOperation');
  const before = work.library.calls.filter(call => call === 'getCritiqueOperation').length;
  await detail.checkCritiqueStatus();
  await detail.checkCritiqueStatus();
  expect(work.library.calls.filter(call => call === 'getCritiqueOperation')).toHaveLength(before + 1);
  operation.status = 'Failed'; operation.message = 'The service is unavailable. Try again later.';
  work.library.release('getCritiqueOperation');
  await detail.expectCritiqueStatus('Failed', operation.message);
});

test('L2-033.2/L2-043: departure ignores late status reads and unavailable resources stop polling', async ({ page }) => {
  await page.clock.install();
  const { work, operation, detail } = await open(page);
  await detail.expectCritiqueStatus('Queued', operation.message);
  work.library.pause('getCritiqueOperation');
  await detail.checkCritiqueStatus();
  await detail.returnToLibrary();
  await work.expectPhotographs(1);
  const before = work.library.calls.length;
  work.library.release('getCritiqueOperation');
  await page.clock.fastForward(15000);
  expect(work.library.calls).toHaveLength(before);
  await work.openPhotograph('Study 01');
  await detail.expectCritiqueStatus('Queued', operation.message);
  work.library.errors.getCritiqueOperation = ['item_unavailable'];
  await detail.checkCritiqueStatus();
  await detail.expectCritiqueStatusUnavailable();
  const after = work.library.calls.length;
  await page.clock.fastForward(15000);
  expect(work.library.calls).toHaveLength(after);
});
