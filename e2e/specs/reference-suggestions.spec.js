import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';

async function setup(page, seed = true) {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(1);
  const item = inspiration.library.items[0], analysis = inspiration.library.analysis;
  if (seed) analysis.seed(item.id);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue(); await inspiration.open(); await inspiration.openReference('Reference 01');
  return { detail: new ReferenceDetailPage(page), analysis, item, signIn };
}

test('Pending suggestions remain separate until edited acceptance; bulk review supports Undo', async ({ page }) => {
  const { detail, item, analysis, signIn } = await setup(page); const notes = item.notes;
  await detail.expectSuggestions('Nothing applies until you accept it.'); await detail.expectPendingTag('soft light'); expect(item.tags).toEqual([]);
  await detail.editSuggestion('My quiet room with window light.'); await detail.expectUnload(true);
  await detail.reviewSuggestions('Accept description'); await detail.expectText('description', 'My quiet room with window light.');
  expect(item.descriptionProvenance).toBe('edited-ai');
  await detail.reviewSuggestions('Accept all'); await detail.expectSuggestions('Reviewed · 2 of 2 tags accepted');
  expect(item.tags).toHaveLength(2); expect(item.notes).toBe(notes);
  await detail.undoSuggestions(); await detail.expectPendingTag('soft light'); expect(item.tags).toEqual([]);
  await detail.reviewSuggestions('Dismiss all'); await detail.expectSuggestions('Reviewed · 0 of 2 tags accepted');
  await page.reload(); await signIn.continue(); await detail.expectSuggestions('Reviewed · 0 of 2 tags accepted');
  expect(analysis.saved.get(item.id).tags.every(tag => tag.state === 'dismissed')).toBe(true);
});

test('Failed review retains the decision and edited description for retry', async ({ page }) => {
  const { detail, item, analysis } = await setup(page);
  await detail.editSuggestion('An edited description.'); analysis.errors.push('request_failed');
  await detail.reviewSuggestions('Accept description'); await detail.expectSuggestionError('Your decision has not been confirmed.'); expect(item.description).toBeNull();
  await detail.reviewSuggestions('Retry review'); await detail.expectText('description', 'An edited description.');
  await detail.reviewSuggestions('Dismiss tag quiet'); await detail.expectPendingTag('quiet', false); expect(item.tags).toEqual([]);
});

test('An image can request suggestions and show background progress without disabling notes', async ({ page }) => {
  const { detail, analysis, item } = await setup(page, false);
  await detail.reviewSuggestions('Generate suggestions'); await detail.expectSuggestions('Looking at the image…');
  await detail.editText('notes', 'Keep working while the image is analyzed.'); await detail.saveText('notes'); await detail.expectTextSaved('notes');
  analysis.seed(item.id); analysis.operations.get(item.id).status = 'Succeeded';
  await detail.expectPendingTag('soft light'); expect(item.tags).toEqual([]);
});

test('Suggested tags can be edited and categorized before accepting, with failed drafts retained', async ({ page }) => {
  const { detail, item, analysis } = await setup(page);
  await detail.editSuggestedTag('soft light', '  North window  ', 'technique'); await detail.expectUnload(true);
  analysis.errors.push('request_failed'); await detail.reviewSuggestions('Accept edited tag');
  await detail.expectSuggestionError('Your decision has not been confirmed.'); await detail.expectSuggestedTagEditor('  North window  '); expect(item.tags).toEqual([]);
  await detail.reviewSuggestions('Retry review'); await detail.expectPendingTag('soft light', false);
  expect(item.tags).toEqual([{ name: 'North window', category: 'technique', provenance: 'edited-ai' }]);
});

test('A stale review retains its edited description and requires explicit latest review', async ({ page }) => {
  const { detail, item } = await setup(page);
  await detail.editSuggestion('My pending edited description.'); item.description = 'Changed in another tab'; item.revision++;
  await detail.reviewSuggestions('Accept description'); await detail.expectSuggestionError('This reference changed.');
  await detail.reviewSuggestions('Review latest metadata'); await detail.expectText('description', 'Changed in another tab');
  await detail.reviewSuggestions('Retry review'); await detail.expectText('description', 'My pending edited description.');
});

for (const width of [1440, 768, 375]) test(`Suggestion review is accessible at ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 900 });
  const { detail } = await setup(page); await detail.expectPendingTag('soft light'); await detail.expectSuggestionsAccessible();
});
