import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';

async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(1);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await inspiration.open(); await inspiration.openReference('Reference 01');
  return { inspiration, signIn, detail: new ReferenceDetailPage(page) };
}

test('manual tags persist after reload, filter the library, and can be removed', async ({ page }) => {
  const { inspiration, detail, signIn } = await setup(page);
  await detail.addTag('soft light'); await detail.expectTag('soft light');
  await page.reload(); await signIn.continue(); await detail.expectTag('soft light');
  await inspiration.open(); await inspiration.filterTag('soft light'); await inspiration.expectReferences(1);
  await inspiration.openReference('Reference 01'); await detail.removeTag('soft light'); await detail.expectNoTag('soft light');
});

test('a failed tag save keeps the requested change and retries', async ({ page }) => {
  const { inspiration, detail } = await setup(page);
  inspiration.library.failures.setTags = 1;
  await detail.addTag('backlight'); await detail.expectTagFailure();
  await detail.retryTags(); await detail.expectTag('backlight');
  await detail.expectSaved(inspiration.library.items[0]);
});

test('An active tag can be renamed and categorized without losing other tag provenance', async ({ page }) => {
  const { inspiration, detail, signIn } = await setup(page);
  await detail.addTag('soft light'); await detail.addTag('portrait');
  inspiration.library.items[0].tags[1].provenance = 'ai-accepted';
  await page.reload(); await signIn.continue();
  await detail.editActiveTag('soft light', 'Window light', 'lighting'); await detail.expectUnload(true); await detail.saveTagEdit();
  await detail.expectTag('Window light'); await detail.expectNoTag('soft light');
  const tags = inspiration.library.items[0].tags;
  expect(tags.find(tag => tag.name === 'Window light')).toEqual({ name: 'Window light', category: 'lighting', provenance: 'manual' });
  expect(tags.find(tag => tag.name === 'portrait').provenance).toBe('ai-accepted');
  await detail.expectSuggestionsAccessible();
});

test('A failed or stale tag edit keeps its name/category until explicitly retried', async ({ page }) => {
  const { inspiration, detail } = await setup(page); await detail.addTag('quiet');
  await detail.editActiveTag('quiet', 'Contemplative', 'mood'); inspiration.library.failures.setTags = 1;
  await detail.saveTagEdit(); await detail.expectTagFailure(); await detail.expectTagEdit('Contemplative', 'mood');
  inspiration.library.items[0].revision++;
  await detail.retryTags(); await detail.expectTagConflict(); await detail.loadLatestTags();
  await detail.expectTagEdit('Contemplative', 'mood'); await detail.retryTags(); await detail.expectTag('Contemplative');
});
