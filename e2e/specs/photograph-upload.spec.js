import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// Given an authenticated empty library, when a photograph is saved with optional
// fields, then acknowledgment opens its saved detail and a reload retains it.
test('L2-001.1/.3: upload without optional fields uses the filename and survives reload', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.save();
  const detail = new PhotographDetailPage(page);
  await detail.expectImage('Morning');
  await detail.expectAbsentContext();
  await page.reload();
  await signIn.continue();
  await detail.expectImage('Morning');
  expect(work.library.photos).toHaveLength(1);
  expect(work.library.calls.filter(call => call === 'upload')).toHaveLength(1);
});

test('L2-001.1/L2-002.1: title and complete brief are normalized and saved', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.fill({ title: '  Morning light  ', intent: '  Explore shadows  ', genre: ' Street ', experience: 'Intermediate', requestedFeedback: '  Look at the edges  ' });
  await upload.save();
  const detail = new PhotographDetailPage(page);
  await detail.expectImage('Morning light');
  await detail.expectBriefValues({ intent: 'Explore shadows', genre: 'Street', experience: 'Intermediate', requestedFeedback: 'Look at the edges' });
  await page.reload();
  await signIn.continue();
  await detail.expectImage('Morning light');
  await detail.expectBriefValues({ intent: 'Explore shadows', genre: 'Street' });
});

test('L2-043: upload waits for acknowledgment and blocks duplicate submission', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  work.library.pause('upload');
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.save();
  await upload.expectSaving();
  expect(work.library.photos).toHaveLength(0);
  expect(work.library.calls.filter(call => call === 'upload')).toHaveLength(1);
  work.library.release('upload');
  await new PhotographDetailPage(page).expectImage('Morning');
  expect(work.library.photos).toHaveLength(1);
});
