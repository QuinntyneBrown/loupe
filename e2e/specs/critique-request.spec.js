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

// Given a saved photograph, when critique is requested, then admission is acknowledged
// before work is claimed queued, with one operation and the saved inputs preserved.
test('L2-008.1/L2-033.1: request waits for admission, then shows durable queued status while notes remain editable', async ({ page }) => {
  const { work, detail } = await open(page);
  await detail.editNotes('A private unsaved observation');
  work.library.pause('requestCritique');
  await detail.requestCritique();
  await detail.expectRequestingCritique();
  expect(work.library.calls.filter(call => call === 'requestCritique')).toHaveLength(1);
  expect(work.library.critiqueOperations.size).toBe(0);
  work.library.release('requestCritique');
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  await detail.expectNotes('A private unsaved observation');
  await detail.saveNotes();
  await detail.returnToLibrary();
  await work.openPhotograph('Study 01');
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueReceipts.size).toBe(1);
  expect(work.library.critiqueRequests[0]).toMatchObject({ revision: 1, regenerate: false });
  expect(work.library.critiqueRequests[0]).not.toHaveProperty('notes');
});

test('L2-030/L2-008.1: retry a lost admission response with the same key and original revision', async ({ page }) => {
  const { work, detail } = await open(page);
  work.library.lostCritiqueResponses = 1;
  await detail.requestCritique();
  await detail.expectCritiqueRequestFailure();
  await detail.editNotes('Saved after the uncertain response');
  await detail.saveNotes();
  await detail.retryCritiqueRequest();
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueReceipts.size).toBe(1);
  expect(work.library.critiqueRequests).toHaveLength(2);
  expect(work.library.critiqueRequests[1]).toEqual(work.library.critiqueRequests[0]);
});

test('L2-002.3/L2-008.1: save brief edits before admitting their critique', async ({ page }) => {
  const { work, detail } = await open(page);
  await detail.editBrief();
  await detail.fillBrief({ intent: 'Deliberately blur the background' });
  await detail.expectCritiqueRequestBlocked();
  expect(work.library.critiqueRequests).toHaveLength(0);
  await detail.saveBrief();
  await detail.requestCritique();
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueRequests[0].revision).toBe(2);
});

test('L2-036.2: unavailable AI leaves the photograph and manual editing usable', async ({ page }) => {
  const { work, detail } = await open(page);
  work.library.errors.requestCritique = ['integration_not_configured'];
  await detail.requestCritique();
  await detail.expectCritiqueRequestFailure('AI critique is not configured. Your photograph is saved and you can keep editing it.');
  await detail.editNotes('Keep working without AI');
  await detail.saveNotes();
  expect(work.library.critiqueOperations.size).toBe(0);
});

test('L2-030/L2-043: a status outage does not discard an uncertain admission key', async ({ page }) => {
  await page.clock.install();
  const { work, detail } = await open(page);
  work.library.lostCritiqueResponses = 1;
  await detail.requestCritique();
  await detail.expectCritiqueRequestFailure();
  work.library.failures.getCritiqueOperation = 1;
  await detail.checkCritiqueStatus();
  await detail.expectCritiqueStatusFailure();
  // Return an older empty status snapshot while retaining the committed receipt.
  work.library.critiqueOperations.clear();
  await detail.checkCritiqueStatus();
  await detail.expectCritiqueRequestFailure();
  const original = [...work.library.critiqueReceipts.values()][0].operation;
  work.library.critiqueOperations.set(original.resourceId, original);
  await detail.retryCritiqueRequest();
  await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueRequests).toHaveLength(2);
  expect(work.library.critiqueRequests[1]).toEqual(work.library.critiqueRequests[0]);
  expect(work.library.critiqueReceipts.size).toBe(1);
});

for (const editing of [false, true]) {
  test(`L2-045.1: admission preserves a useful keyboard position with editing=${editing}`, async ({ page }) => {
    const { work, detail } = await open(page);
    work.library.pause('requestCritique');
    await detail.requestCritique();
    await detail.expectRequestingCritique();
    if (editing) { await detail.editNotes('Continue thinking while admission waits'); await detail.focusNotes(); }
    work.library.release('requestCritique');
    await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
    if (editing) await detail.expectNotesFocus();
    else await detail.expectCritiqueStatusFocus();
  });
}

for (const width of [320, 375, 1440]) {
  test(`L2-044/045: critique actions remain distinct and fit at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const { detail } = await open(page);
    await detail.expectNoCritique();
    await detail.expectCritiqueRequestLayout();
  });
}
