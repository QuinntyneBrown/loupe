import { test } from '@playwright/test';
import { SearchPage } from '../page-objects/search-page.js';

test('given a held synthetic request, loading completes into real matching cards', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.chooseState('Loading');
  await search.expectLoading();
  await search.completeLoading();
  await search.expectResults(['Morning by the window', 'Mara Lindqvist', 'Window study without a preview', 'Studio sitting no. 4']);
  await search.chooseState('Empty');
  await search.expectEmpty('neon rain');
  await search.chooseState('Initial');
  await search.expectInitial();
});

test('given a failed synthetic request, retry preserves the query and returns matching cards', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.chooseState('Failure');
  await search.expectFailure();
  await search.retry();
  await search.expectResults(['Morning by the window', 'Mara Lindqvist', 'Window study without a preview', 'Studio sitting no. 4']);
  await search.expectResultFocused('Morning by the window');
});

test('given a later-page failure, retry keeps the loaded cards and appends each remaining card once', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.chooseState('Pagination recovery');
  await search.expectStatus('4 of 6 results');
  await search.loadMore();
  await search.expectPageFailure();
  await search.retryPage();
  await search.expectResults(['Morning by the window', 'Mara Lindqvist', 'Window study without a preview', 'Studio sitting no. 4', 'At the desk', 'Saoirse Quinn']);
  await search.expectResultFocused('At the desk');
  await search.expectStatus('6 of 6 results');
});

test('given loading, submitting another query never displays stale matching cards', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.chooseState('Loading');
  await search.expectLoading();
  await search.search('architecture');
  await search.expectResults(['Quiet architecture with a long study title about light, geometry, and the spaces between buildings', 'Inez Okafor']);
  await search.expectQuery('architecture');
  await search.expectStatus('2 of 2 results for “architecture”');
});
test('given a pending next page, typing a new query keeps focus when the saved search finishes', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.search('window');
  await search.expectStatus('4 of 6 results');
  await search.loadMore();
  await search.typeQuery('architecture');
  await search.expectStatus('6 of 6 results');
  await search.expectQuery('architecture');
  await search.expectQueryFocused();
});
