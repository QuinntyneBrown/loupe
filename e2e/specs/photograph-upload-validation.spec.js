import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { PhotographUploadPage } from '../page-objects/photograph-upload-page.js';

// L2-001.2/L2-002.2: Given invalid upload fields, when corrected, then the draft
// remains and only a valid submission reaches the service. Limits count scalars.
for (const [field, maximum] of [['title', 200], ['intent', 2000], ['genre', 100], ['requestedFeedback', 2000]]) {
  test(`L2-002.2: upload ${field} enforces its normalized Unicode boundary`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(0);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    const upload = new PhotographUploadPage(page);
    await upload.open();
    await upload.chooseImage();
    const boundary = '📷'.repeat(maximum);
    await upload.fill({ [field]: boundary + 'x' });
    await upload.expectFieldLimit(field, maximum);
    expect(work.library.calls).not.toContain('upload');
    await upload.fill({ [field]: '  ' + boundary + '  ' });
    await upload.save();
    await upload.viewCompletedPhotograph();
  const detail = new PhotographDetailPage(page);
    await detail.expectImage(field === 'title' ? boundary : 'Morning');
    if (field !== 'title') await detail.expectBriefValues({ [field]: boundary });
  });
}

for (const [size, message] of [[0, 'Choose an image that is not empty.'], [25000001, 'Choose an image of 25 MB or less.']]) {
  test(`L2-001.2: ${size}-byte upload is rejected before submission and can be corrected`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(0);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    const upload = new PhotographUploadPage(page);
    await upload.open();
    await upload.fill({ intent: 'Keep my brief' });
    await upload.chooseFile({ size });
    await upload.expectFileError(message);
    expect(work.library.calls).not.toContain('upload');
    await upload.chooseImage();
    await upload.save();
    await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectBriefValues({ intent: 'Keep my brief' });
  });
}

test('L2-001.2: the 25 MB boundary can be submitted', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseFile({ size: 25000000 });
  await upload.save();
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage('Morning');
});

test('L2-039.1: unsupported declared file types are explained without losing the brief', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.fill({ intent: 'Keep my brief' });
  await upload.chooseFile({ name: 'Drawing.svg', mimeType: 'image/svg+xml' });
  await upload.expectFileError('Choose a still JPEG, PNG, HEIC or WebP image.');
  expect(work.library.calls).not.toContain('upload');
  await upload.chooseImage();
  await upload.save();
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectBriefValues({ intent: 'Keep my brief' });
});
