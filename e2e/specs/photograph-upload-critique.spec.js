import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

async function open(page) {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  const upload = new PhotographUploadPage(page); await upload.open(); await upload.chooseImage();
  await upload.requestCritiqueAfterSaving();
  return { work, upload, detail: new PhotographDetailPage(page) };
}

// Given upload-and-critique, when the photograph persists, then critique admission
// happens afterward; any admission failure keeps the photograph independently saved.
test('L2-008.2: save first, then admit critique and open its durable status', async ({ page }) => {
  const { work, upload, detail } = await open(page);
  work.library.pause('upload'); work.library.pause('requestCritique');
  await upload.save(); await upload.expectSaving();
  expect(work.library.calls).not.toContain('requestCritique');
  work.library.release('upload');
  await upload.expectSavedBeforeCritique(); await upload.expectCritiquePending();
  expect(work.library.photos).toHaveLength(1);
  work.library.release('requestCritique');
  await detail.expectImage('Morning'); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.calls.filter(call => call === 'upload')).toHaveLength(1);
});

test('L2-008.2/L2-036: rejected admission keeps the uploaded photograph available for later critique', async ({ page }) => {
  const { work, upload, detail } = await open(page);
  work.library.errors.requestCritique = ['integration_not_configured'];
  await upload.save(); await upload.expectSavedBeforeCritique(); await upload.expectCritiqueNotQueued();
  expect(work.library.photos).toHaveLength(1);
  await upload.viewSavedPhotograph(); await detail.expectImage('Morning');
  await detail.requestCritique(); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.calls.filter(call => call === 'upload')).toHaveLength(1);
});

test('L2-008.2/L2-030: a lost admission reply retries the same request without another upload', async ({ page }) => {
  const { work, upload, detail } = await open(page);
  work.library.lostCritiqueResponses = 1;
  await upload.save(); await upload.expectSavedBeforeCritique(); await upload.expectUncertainCritique();
  await upload.retryCritique(); await detail.expectImage('Morning'); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.critiqueRequests[1]).toEqual(work.library.critiqueRequests[0]);
  expect(work.library.critiqueReceipts.size).toBe(1);
  expect(work.library.calls.filter(call => call === 'upload')).toHaveLength(1);
});

test('L2-008.2/L2-043: failed upload retains the critique choice and requests only after upload acknowledgment', async ({ page }) => {
  const { work, upload, detail } = await open(page);
  work.library.errors.upload = ['request_failed'];
  await upload.save(); await upload.expectFailure();
  expect(work.library.calls).not.toContain('requestCritique');
  await upload.retry(); await detail.expectImage('Morning'); await detail.expectCritiqueStatus('Queued', 'Waiting to start.');
  expect(work.library.photos).toHaveLength(1);
  expect(work.library.critiqueRequests).toHaveLength(1);
});
