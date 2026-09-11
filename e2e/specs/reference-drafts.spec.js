import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';

async function setup(page) {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(0);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await inspiration.open();
  return inspiration;
}

test('Upload remains a preview until the edited reference is explicitly saved', async ({ page }) => {
  const inspiration = await setup(page);
  await inspiration.openSave();
  await inspiration.uploadDraft();
  expect(inspiration.library.items).toHaveLength(0);
  await inspiration.editDraft('Window study', 'Casey Example');
  await inspiration.saveDraft();
  await expect.poll(() => inspiration.library.items.length).toBe(1);
  expect(inspiration.library.items[0].title).toBe('Window study');
  expect(inspiration.library.items[0].attribution).toBe('Casey Example');
  await inspiration.expectSaveClosed();
});

test('Cancel discards an upload preview without saving a reference', async ({ page }) => {
  const inspiration = await setup(page);
  await inspiration.openSave();
  await inspiration.uploadDraft();
  await inspiration.cancelDraft();
  expect(inspiration.library.items).toHaveLength(0);
  expect(inspiration.library.drafts.size).toBe(0);
});
