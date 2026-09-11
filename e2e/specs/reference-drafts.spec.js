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

test('A link is imported into an editable preview before final Save', async ({ page }) => {
  const inspiration = await setup(page);
  await inspiration.openSave(); await inspiration.importDraft('https://source.example/work');
  await inspiration.expectDraftPreview();
  expect(inspiration.library.items).toHaveLength(0);
  await inspiration.editDraft('Edited imported title', 'Corrected photographer'); await inspiration.saveDraft();
  await expect.poll(() => inspiration.library.items.length).toBe(1);
  expect(inspiration.library.items[0].sourceUrl).toBe('https://source.example/work');
  expect(inspiration.library.items[0].attribution).toBe('Corrected photographer');
});

test('A restricted source can be saved as a link with personal notes', async ({ page }) => {
  const inspiration = await setup(page); inspiration.library.draftFailure = 'robots_disallowed';
  await inspiration.openSave(); await inspiration.importDraft('https://source.example/restricted');
  await inspiration.expectImportFallback();
  expect(inspiration.library.items).toHaveLength(0);
  await inspiration.saveFallback('Keep this source', 'Study the lighting later.');
  await expect.poll(() => inspiration.library.items.length).toBe(1);
  expect(inspiration.library.items[0].notes).toBe('Study the lighting later.');
  expect(inspiration.library.items[0].previewUrl).toBeNull();
});

test('Cancel while importing prevents a late preview from opening', async ({ page }) => {
  const inspiration = await setup(page); inspiration.library.pause('draft-get');
  await inspiration.openSave(); await inspiration.importDraft('https://source.example/work');
  await inspiration.expectImporting();
  await inspiration.cancelDraft(); inspiration.library.release('draft-get');
  await inspiration.expectSaveClosed();
  expect(inspiration.library.items).toHaveLength(0); expect(inspiration.library.drafts.size).toBe(0);
});

test('A saved source shows the existing reference without creating a duplicate', async ({ page }) => {
  const inspiration = await setup(page);
  inspiration.library.items.push({ ...new (inspiration.library.constructor)(1).items[0] });
  await inspiration.openSave(); await inspiration.importDraft('https://source.example/photo');
  await inspiration.expectDraftDuplicate();
  expect(inspiration.library.items).toHaveLength(1);
});
