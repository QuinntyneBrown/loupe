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

test('Save from a board preselects it and supports a new board in the preview', async ({ page }) => {
  const inspiration = await setup(page);
  await inspiration.newBoard('Window light'); await inspiration.selectBoard('Window light');
  await inspiration.openSave(); await inspiration.uploadDraft();
  await inspiration.expectDraftBoardSelected('Window light'); await inspiration.newDraftBoard('Colour studies');
  await inspiration.saveDraft(); await expect.poll(() => inspiration.library.items.length).toBe(1);
  expect(inspiration.library.items[0].boardIds).toHaveLength(2);
  await inspiration.expectBoard('Window light', 1); await inspiration.expectBoard('Colour studies', 1);
});

test('Back discards the temporary preview while keeping the source form available', async ({ page }) => {
  const inspiration = await setup(page);
  await inspiration.openSave(); await inspiration.uploadDraft(); await inspiration.backFromDraft();
  expect(inspiration.library.drafts.size).toBe(0); expect(inspiration.library.items).toHaveLength(0);
});

test('A manual fallback image retains the original source, edited title and notes', async ({ page }) => {
  const inspiration = await setup(page); inspiration.library.draftFailure = 'robots_disallowed';
  await inspiration.openSave(); await inspiration.importDraft('https://source.example/restricted');
  await inspiration.expectImportFallback(); await inspiration.addFallbackImage('Manual image', 'Keep this context.');
  await inspiration.saveDraft(); await expect.poll(() => inspiration.library.items.length).toBe(1);
  const saved = inspiration.library.items[0];
  expect(saved.title).toBe('Manual image'); expect(saved.notes).toBe('Keep this context.');
  expect(saved.sourceUrl).toBe('https://source.example/restricted'); expect(saved.previewUrl).not.toBeNull();
});

for (const width of [375, 768, 1440]) test(`Save dialog remains accessible at ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 900 });
  const inspiration = await setup(page);
  await inspiration.openSave(); await inspiration.expectDraftAccessible();
  await inspiration.uploadDraft(); await inspiration.expectDraftAccessible();
});

test('Retry after a lost final-save response preserves one reference and one new board', async ({ page }) => {
  const inspiration = await setup(page); inspiration.library.lostDraftResponses = 1;
  await inspiration.openSave(); await inspiration.uploadDraft(); await inspiration.newDraftBoard('Window study'); await inspiration.saveDraft();
  await inspiration.expectDraftError('The reference could not be saved. Your preview is still here. Try again.');
  await inspiration.saveDraft(); await inspiration.expectSaveClosed();
  expect(inspiration.library.items).toHaveLength(1); expect(inspiration.library.boards).toHaveLength(1);
});

test('Cancel overlapping a fallback image response discards both temporary drafts', async ({ page }) => {
  const inspiration = await setup(page); inspiration.library.draftFailure = 'robots_disallowed';
  await inspiration.openSave(); await inspiration.importDraft('https://source.example/restricted'); await inspiration.expectImportFallback();
  inspiration.library.pause('draft-upload'); inspiration.library.pause('draft-cancel');
  await inspiration.chooseFallbackImage(); const cancel = inspiration.cancelDraft();
  await expect.poll(() => inspiration.library.draftCalls.some(call => call.operation === 'cancel')).toBe(true);
  inspiration.library.release('draft-upload'); await expect.poll(() => inspiration.library.drafts.size).toBe(2);
  inspiration.library.release('draft-cancel'); await cancel;
  await expect.poll(() => inspiration.library.drafts.size).toBe(0); expect(inspiration.library.items).toHaveLength(0);
});

test('Upload transfer progress waits for the preview acknowledgment', async ({ page }) => {
  const inspiration = await setup(page); inspiration.library.pause('draft-upload');
  await inspiration.openSave(); await inspiration.startUploadDraft();
  await inspiration.reportDraftProgress(64, 128); await inspiration.expectDraftProgress(64, 128);
  expect(inspiration.library.items).toHaveLength(0);
  inspiration.library.release('draft-upload'); await inspiration.expectDraftPreview();
});

test('A source saved elsewhere during preview shows the existing reference at final Save', async ({ page }) => {
  const inspiration = await setup(page);
  await inspiration.openSave(); await inspiration.importDraft('https://source.example/work'); await inspiration.expectDraftPreview();
  const existing = { ...new (inspiration.library.constructor)(1).items[0], sourceUrl: 'https://source.example/work' }; inspiration.library.items.push(existing);
  await inspiration.saveDraft(); await inspiration.expectDraftDuplicate(); expect(inspiration.library.items).toEqual([existing]);
});

test('Browser navigation offers Keep editing or Discard for an unsaved preview', async ({ page }) => {
  const inspiration = await setup(page);
  await inspiration.openSave(); await inspiration.expectDraftUnload(false); await inspiration.uploadDraft(); await inspiration.expectDraftUnload(true);
  await inspiration.backInBrowser(); await inspiration.expectDiscardDraft(); await inspiration.keepDraft(); await inspiration.expectDraftPreview();
  await inspiration.backInBrowser(); await inspiration.expectDiscardDraft(); await inspiration.discardDraftNavigation();
  await expect(page).toHaveURL(/\/my-work$/); await inspiration.expectDraftUnload(false);
  expect(inspiration.library.items).toHaveLength(0); expect(inspiration.library.drafts.size).toBe(0);
});

test('Leaving a pending final Save warns and ignores its late response', async ({ page }) => {
  const inspiration = await setup(page); inspiration.library.pause('draft-save');
  await inspiration.openSave(); await inspiration.uploadDraft(); await inspiration.saveDraft();
  await expect.poll(() => inspiration.library.draftCalls.some(call => call.operation === 'save')).toBe(true);
  await inspiration.backInBrowser(); await inspiration.expectDiscardDraft(); await inspiration.expectPendingDraftWarning(); await inspiration.discardDraftNavigation();
  inspiration.library.release('draft-save'); await expect.poll(() => inspiration.library.items.length).toBe(1);
  await expect(page).toHaveURL(/\/my-work$/);
});

test('The empty library and empty board open Save directly and restore the initiating control', async ({ page }) => {
  const inspiration = await setup(page); await inspiration.expectEmpty();
  await inspiration.saveFromEmpty(); await inspiration.cancelDraft(); await inspiration.expectEmptySaveFocus();
  await inspiration.newBoard('Empty study'); await inspiration.selectBoard('Empty study');
  await inspiration.saveFromEmpty(); await inspiration.uploadDraft(); await inspiration.expectDraftBoardSelected('Empty study');
});
