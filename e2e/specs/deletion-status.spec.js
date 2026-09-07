import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { DeletionPage } from '../page-objects/deletion-page.js';

async function deletePhotograph(page, error) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectSaved();
  if (error) work.library.errors.getDeletion = [error];
  await detail.deletePhotograph();
  await detail.confirmDeletion();
  return { work, deletion: new DeletionPage(page) };
}

// Given durable deletion status, when reads fail or navigation changes,
// then recovery remains honest, single-flight and limited to the active screen.
test('L2-031.6/L2-043: initial service outage retries cleanup status automatically', async ({ page }) => {
  const { work, deletion } = await deletePhotograph(page, 'service_unavailable');
  await deletion.expectFailure();
  work.library.completeDeletion();
  await deletion.expectCompleted();
});

test('L2-031.6/L2-043: failed refresh retains pending state and repeated checks send one request', async ({ page }) => {
  const { work, deletion } = await deletePhotograph(page);
  await deletion.expectPending();
  work.library.failures.getDeletion = 1;
  await deletion.checkStatus();
  await deletion.expectFailure();
  await deletion.expectPendingMessage();
  work.library.pause('getDeletion');
  const before = work.library.calls.filter(call => call === 'getDeletion').length;
  await deletion.checkStatus();
  await deletion.checkStatus();
  await deletion.checkStatus();
  expect(work.library.calls.filter(call => call === 'getDeletion')).toHaveLength(before + 1);
  work.library.completeDeletion();
  work.library.release('getDeletion');
  await deletion.expectCompleted();
});

for (const outcome of ['Completed', 'Unavailable']) {
  test(`L2-031.5/6: ${outcome} cleanup status stops automatic polling`, async ({ page }) => {
    await page.clock.install();
    const { work, deletion } = await deletePhotograph(page);
    await deletion.expectPending();
    if (outcome === 'Completed') work.library.completeDeletion();
    else work.library.deletions.clear();
    await deletion.checkStatus();
    if (outcome === 'Completed') await deletion.expectCompleted();
    else await deletion.expectUnavailable();
    const before = work.library.calls.filter(call => call === 'getDeletion').length;
    await page.clock.fastForward(15000);
    expect(work.library.calls.filter(call => call === 'getDeletion')).toHaveLength(before);
  });
}

test('L2-031.6/L2-043: departure ignores an outstanding status response and stops polling', async ({ page }) => {
  await page.clock.install();
  const { work, deletion } = await deletePhotograph(page);
  await deletion.expectPending();
  work.library.pause('getDeletion');
  await deletion.checkStatus();
  const before = work.library.calls.filter(call => call === 'getDeletion').length;
  await deletion.backToMyWork();
  await work.expectEmpty();
  work.library.completeDeletion();
  work.library.release('getDeletion');
  await page.clock.fastForward(15000);
  await work.expectEmpty();
  expect(work.library.calls.filter(call => call === 'getDeletion')).toHaveLength(before);
});
