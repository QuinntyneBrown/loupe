import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';

async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(1);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await inspiration.open(); await inspiration.openReference('Reference 01');
  const detail = new ReferenceDetailPage(page); await detail.openImageReplacement();
  return { detail, library: inspiration.library, item: inspiration.library.items[0] };
}

test('Given a saved reference, replacement changes its image only after Save image', async ({ page }) => {
  const { detail, library, item } = await setup(page); const before = { ...item };
  await detail.chooseReplacement(); expect(library.calls).not.toContain('replaceImage');
  await detail.saveImage(); await detail.expectImageDialogClosed(); await detail.expectSaved(item);
  expect(item.imageUrl).not.toBe(before.imageUrl);
  for (const field of ['title', 'notes', 'attribution', 'sourceUrl', 'boardIds', 'tags']) expect(item[field]).toEqual(before[field]);
});

test('Cancel and unsupported files leave the existing image intact', async ({ page }) => {
  const { detail, library, item } = await setup(page); const image = item.imageUrl;
  await detail.chooseReplacement('application/pdf'); await detail.expectImageError('Choose a JPEG, PNG, HEIC or WebP image.'); await detail.expectImageSaveDisabled();
  await detail.chooseReplacement(); await detail.cancelImage(); await detail.expectImageDialogClosed();
  expect(library.calls).not.toContain('replaceImage'); expect(item.imageUrl).toBe(image);
});

test('A failed replacement retains the chosen file and retries', async ({ page }) => {
  const { detail, library, item } = await setup(page); library.failures.replaceImage = 1;
  await detail.chooseReplacement(); await detail.saveImage(); await detail.expectImageError('The image save was not confirmed. Your chosen file is still here. Try again.');
  await detail.saveImage(); await detail.expectImageDialogClosed(); await detail.expectSaved(item);
});

test('Stale replacement requires reviewing the latest reference before retry', async ({ page }) => {
  const { detail, item } = await setup(page); await detail.chooseReplacement(); item.revision++;
  await detail.saveImage(); await detail.expectImageError('This reference changed. Review the latest reference before replacing its image.'); await detail.expectImageSaveDisabled();
  await detail.reloadImageReference(); await detail.saveImage(); await detail.expectImageDialogClosed(); await detail.expectSaved(item);
});
