import { test, expect } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';
import { critiqueOperation } from '../fixtures/critique-operation.js';

async function open(page, failureCode = 'provider_timeout', retryAvailableAt = null) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const id = work.library.photos[0].id;
  work.library.critiques.set(id, savedCritique());
  const source = { ...critiqueOperation(id, 'Failed'), failureCode, retryAvailableAt, message: 'Analysis could not be completed.' };
  work.library.critiqueOperations.set(id, source);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination(); await signIn.continue(); await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectCritique(); await detail.expectCritiqueStatus('Failed', source.message);
  return { work, detail, id, source };
}

// Given retryable failed work, when Retry is selected, then admission reuses
// original inputs and preserves the previous critique and private notes.
for (const code of ['invalid_output', 'provider_timeout', 'worker_interrupted', 'provider_unavailable', 'provider_rate_limited']) {
  test(`L2-034.2/5: retry ${code} after acknowledgment while preserving content`, async ({ page }) => {
    const { work, detail, source } = await open(page, code);
    await detail.editNotes('Preserve this private draft');
    work.library.pause('retryCritique');
    await detail.retryFailedCritique(); await detail.expectCritiqueRetryPending();
    await detail.expectCritique(); await detail.expectCritiqueStatus('Failed', source.message);
    work.library.release('retryCritique');
    await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
    await detail.expectCritiqueStatusFocus(); await detail.expectNotes('Preserve this private draft');
    expect(work.library.critiqueRetries[0]).toMatchObject({ operationId: source.id, revision: 1 });
    expect(work.library.critiqueRetryReceipts.size).toBe(1);
  });
}

test('L2-030/L2-034.4: uncertain manual retry replays one key after a notes save', async ({ page }) => {
  const { work, detail } = await open(page);
  work.library.lostCritiqueRetryResponses = 1;
  await detail.retryFailedCritique(); await detail.expectCritiqueRequestFailure();
  await detail.editNotes('Save while admission is uncertain'); await detail.saveNotes();
  await detail.retryCritiqueRequest(); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueRetries[1]).toEqual(work.library.critiqueRetries[0]);
  expect(work.library.critiqueRetryReceipts.size).toBe(1);
});

test('L2-034.4: changed inputs require review and an explicitly new critique', async ({ page }) => {
  const { work, detail } = await open(page);
  work.library.errors.retryCritique = ['analysis_inputs_changed'];
  await detail.retryFailedCritique();
  await detail.expectCritiqueRequestFailure('The image, brief, or AI configuration changed. Review the saved brief before requesting a new critique.');
  work.library.photos[0].brief.intent = 'Retain the intentional blur'; work.library.photos[0].revision = 2;
  await detail.reviewCritiqueRequest(); await detail.expectLatestCritiqueBrief('Retain the intentional blur');
  expect(work.library.critiqueRequests).toHaveLength(0);
  await detail.requestNewCritiqueWithBrief(); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueRequests[0]).toMatchObject({ revision: 2, regenerate: true });
  await detail.expectCritique();
});

test('L2-030/L2-034.4: stale retry reviews the current revision without silently becoming new analysis', async ({ page }) => {
  const { work, detail, source } = await open(page);
  work.library.photos[0].revision = 2;
  await detail.retryFailedCritique();
  await detail.expectCritiqueRequestFailure('This photograph changed. Review its latest saved brief before requesting.');
  await detail.reviewCritiqueRequest(); await detail.expectLatestCritiqueBrief('Explore quiet morning light');
  await detail.retryWithReviewedBrief(); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueRetries[1]).toMatchObject({ revision: 2, operationId: source.id });
  expect(work.library.critiqueRequests).toHaveLength(0);
});

test('L2-034.4: provider retry delay expires before Retry is enabled', async ({ page }) => {
  await page.clock.install({ time: new Date('2026-09-08T10:00:00Z') });
  const { detail } = await open(page, 'provider_rate_limited', '2026-09-08T10:00:10Z');
  await detail.expectFailedRetryDisabled();
  await page.clock.fastForward(11000);
  await detail.retryFailedCritique(); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
});

for (const code of ['provider_credentials', 'provider_access_denied', 'provider_disabled']) {
  test(`L2-034.6: ${code} does not offer an immediate failed-job retry`, async ({ page }) => {
    const { detail } = await open(page, code);
    await detail.expectNoFailedRetry(); await detail.expectCritique();
  });
}
