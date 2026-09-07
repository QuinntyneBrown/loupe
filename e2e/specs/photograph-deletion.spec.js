// Given a saved photograph, when its owner confirms deletion, then acknowledgment
// revokes its UI access and opens durable cleanup status; cancellation changes nothing.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { DeletionPage } from '../page-objects/deletion-page.js';

async function openPhotograph(page) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectSaved();
  return { work, detail, signIn, deletion: new DeletionPage(page) };
}

test('L2-031.1: named deletion confirmation can be cancelled without losing a draft', async ({ page }) => {
  const { work, detail } = await openPhotograph(page);
  await detail.editNotes('Unsaved observation');
  await detail.deletePhotograph();
  await detail.expectDeleteConfirmation();
  await detail.cancelDeletion();
  await detail.expectDeletionCancelled();
  await detail.expectNotes('Unsaved observation');
  expect(work.library.calls).not.toContain('deletePhotograph');
  expect(work.library.photos).toHaveLength(1);
});

test('L2-031.1/6: confirmed deletion opens durable pending status and observes completion', async ({ page }) => {
  const { work, detail, signIn, deletion } = await openPhotograph(page);
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  await deletion.expectPending();
  await page.reload();
  await signIn.continue();
  await deletion.expectPending();
  work.library.completeDeletion();
  await deletion.expectCompleted();
  await deletion.backToMyWork();
  await work.expectEmpty();
});

test('L2-031.1: explicit deletion discards unsaved edits without a second navigation prompt', async ({ page }) => {
  const { work, detail, deletion } = await openPhotograph(page);
  await detail.editNotes('Unsaved observation');
  await detail.deletePhotograph();
  await detail.expectDeleteConfirmation();
  await detail.confirmDeletion();
  await deletion.expectPending();
  expect(work.library.calls).not.toContain('updateNotes');
});

test('L2-031.1/5: pending deletion waits for acknowledgment and sends one request', async ({ page }) => {
  const { work, detail, deletion } = await openPhotograph(page);
  work.library.pause('deletePhotograph');
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  await detail.expectDeleting();
  expect(work.library.photos).toHaveLength(1);
  expect(work.library.calls.filter(call => call === 'deletePhotograph')).toHaveLength(1);
  work.library.release('deletePhotograph');
  await deletion.expectPending();
});

test('L2-031.5/6: an unconfirmed deletion offers an explicit retry', async ({ page }) => {
  const { work, detail, deletion } = await openPhotograph(page);
  work.library.failures.deletePhotograph = 1;
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  await detail.expectDeletionFailure();
  expect(work.library.photos).toHaveLength(1);
  await detail.retryDeletion();
  await deletion.expectPending();
});

// Given a stale revision or uncertain response, when deletion is recovered,
// then explicit review protects newer edits and retry resolves one operation.
test('L2-030/L2-031: a changed photograph requires latest-detail review before deletion', async ({ page }) => {
  const { work, detail, deletion } = await openPhotograph(page);
  await detail.editNotes('My unsaved observation');
  work.library.photos[0].notes = 'New saved observation';
  work.library.photos[0].brief.intent = 'New saved intent';
  work.library.photos[0].revision++;
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  await detail.expectDeletionConflict();
  expect(work.library.photos).toHaveLength(1);
  await detail.reviewDeletion();
  await detail.expectDeletionReview('New saved observation', 'New saved intent');
  await detail.confirmDeletion();
  await deletion.expectPending();
  expect(work.library.deletions.size).toBe(1);
  expect(work.library.calls).not.toContain('updateNotes');
});

test('L2-030/L2-043: failed deletion review and cancellation preserve unsaved edits', async ({ page }) => {
  const { work, detail } = await openPhotograph(page);
  await detail.editNotes('My unsaved observation');
  work.library.photos[0].revision++;
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  await detail.expectDeletionConflict();
  work.library.failures.get = 1;
  await detail.reviewDeletion();
  await detail.expectDeletionReviewFailure();
  await detail.reviewDeletion();
  await detail.expectDeletionReview(work.library.photos[0].notes, work.library.photos[0].brief.intent);
  await detail.cancelDeletion();
  await detail.expectDeletionCancelled();
  await detail.expectNotes('My unsaved observation');
  expect(work.library.photos).toHaveLength(1);
});

test('L2-031.5: retry after a lost deletion response returns the existing operation', async ({ page }) => {
  const { work, detail, deletion } = await openPhotograph(page);
  work.library.lostDeleteResponses = 1;
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  await detail.expectDeletionFailure();
  expect(work.library.photos).toHaveLength(0);
  const operation = [...work.library.deletions.keys()][0];
  await detail.retryDeletion();
  await deletion.expectPending();
  expect(work.library.deletions.size).toBe(1);
  expect(page.url()).toContain(operation);
});

test('L2-031.5/L2-043: an unavailable photograph explains recovery and retains unsaved text', async ({ page }) => {
  const { work, detail } = await openPhotograph(page);
  await detail.editNotes('My unsaved observation');
  work.library.photos = [];
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  await detail.expectDeletionUnavailable();
  await detail.cancelDeletion();
  await detail.expectNotes('My unsaved observation');
});

for (const dirty of [false, true]) {
  test(`L2-031/L2-043: pending deletion protects browser departure with dirty=${dirty}`, async ({ page }) => {
    const { work, detail, deletion } = await openPhotograph(page);
    if (dirty) await detail.editNotes('Keep until deletion is confirmed');
    work.library.pause('deletePhotograph');
    await detail.deletePhotograph();
    await detail.confirmDeletion();
    await detail.expectDeleting();
    await detail.expectUnloadProtection(true);
    await page.goBack();
    await detail.expectDeleting();
    await detail.expectNoDiscardChoice();
    work.library.release('deletePhotograph');
    await deletion.expectPending();
    await detail.expectUnloadProtection(false);
  });
}
