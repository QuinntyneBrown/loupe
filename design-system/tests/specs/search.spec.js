import { test } from '@playwright/test';
import { SearchPage } from '../page-objects/search-page.js';

test('given the initial keyword example, submitting words returns matching synthetic mixed cards', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.expectInitial();
  await search.useExample('window');
  await search.expectQuery('window');
  await search.expectResults(['Morning by the window', 'Mara Lindqvist', 'Window study without a preview', 'Studio sitting no. 4']);
  await search.expectStatus('4 of 6 results');
  await search.expectNoPreview('Window study without a preview');
  await search.expectSafeSources();
  await search.openResult('Mara Lindqvist');
  await search.expectDetail('Mara Lindqvist');
  await search.selectType('Photographers');
  await search.expectResults(['Mara Lindqvist', 'Saoirse Quinn']);
  await search.expectQuery('window');
});

test('given a query with no keyword matches, empty state offers editing rather than failure retry', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.search('neon rain');
  await search.expectEmpty('neon rain');
  await search.editQuery();
  await search.search('architecture');
  await search.expectResults(['Quiet architecture with a long study title about light, geometry, and the spaces between buildings', 'Inez Okafor']);
});
