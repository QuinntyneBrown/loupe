import { test } from '@playwright/test';
import { SearchPage } from '../page-objects/search-page.js';

test('given filter-only browsing with no matches, empty feedback names the filters rather than an empty query', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.openFilters();
  await search.toggleFilter('Boards', 'Colour studies');
  await search.applyFilters();
  await search.expectEmpty('');
  await search.removeFilter('board', 'Colour studies');
  await search.expectStatus('4 of 8 results');
  await search.expectQuery('');
});

test('given filter drafts, cancel and Escape discard edits while Apply combines boards and tags with the query', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.search('window');
  await search.expectStatus('4 of 6 results');
  await search.openFilters();
  await search.toggleFilter('Boards', 'Window light');
  await search.toggleFilter('Tags', 'soft light');
  await search.expectSelection('Boards', 'Window light', true);
  await search.expectActiveFilter('board', 'Window light', false);
  await search.cancelFilters();
  await search.expectFilterReturnFocus();
  await search.expectStatus('4 of 6 results');
  await search.openFilters();
  await search.expectSelection('Boards', 'Window light', false);
  await search.toggleFilter('Boards', 'Window light');
  await search.escapeFilters();
  await search.expectFilterReturnFocus();
  await search.openFilters();
  await search.expectSelection('Boards', 'Window light', false);
  await search.toggleFilter('Boards', 'Window light');
  await search.toggleFilter('Tags', 'soft light');
  await search.applyFilters();
  await search.expectFilterReturnFocus();
  await search.expectResults(['Morning by the window']);
  await search.expectActiveFilter('board', 'Window light');
  await search.expectActiveFilter('tag', 'soft light');
});

test('given selected filters, draft Clear is cancelable and applied clearing preserves query and type', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.search('window');
  await search.selectType('References');
  await search.openFilters();
  await search.toggleFilter('Boards', 'Window light');
  await search.toggleFilter('Tags', 'window light');
  await search.toggleFilter('Tags', 'negative space');
  await search.applyFilters();
  await search.expectEmpty('window');
  await search.openFilters();
  await search.clearDraft();
  await search.expectSelection('Boards', 'Window light', false);
  await search.expectActiveFilter('board', 'Window light');
  await search.cancelFilters();
  await search.expectEmpty('window');
  await search.openFilters();
  await search.clearDraft();
  await search.applyFilters();
  await search.expectResults(['Morning by the window', 'Window study without a preview', 'Studio sitting no. 4', 'At the desk']);
  await search.expectQuery('window');
  await search.openFilters();
  await search.toggleFilter('Boards', 'Portrait sittings');
  await search.applyFilters();
  await search.expectResults(['Studio sitting no. 4', 'At the desk']);
  await search.clearFilters();
  await search.expectResults(['Morning by the window', 'Window study without a preview', 'Studio sitting no. 4', 'At the desk']);
  await search.expectQuery('window');
});

test('given photographers plus a board or an unavailable selection, no results explains corrective filter removal', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.search('window');
  await search.selectType('Photographers');
  await search.openFilters();
  await search.toggleFilter('Boards', 'Window light');
  await search.applyFilters();
  await search.expectBoardExplanation();
  await search.removeFilter('board', 'Window light');
  await search.expectResults(['Mara Lindqvist', 'Saoirse Quinn']);
  await search.chooseState('Unavailable filters');
  await search.expectUnavailableBoard();
  await search.openFilters();
  await search.expectSelection('Boards', 'Unavailable board · unavailable', true);
  await search.cancelFilters();
  await search.removeFilter('board', 'Unavailable board');
  await search.expectStatus('4 of 6 results');
});

test('given multiple tags and boards, every tag is required while any board can match', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.search('window');
  await search.openFilters();
  await search.toggleFilter('Tags', 'window light');
  await search.toggleFilter('Tags', 'soft light');
  await search.applyFilters();
  await search.expectResults(['Morning by the window', 'Mara Lindqvist']);
  await search.openFilters();
  await search.toggleFilter('Boards', 'Window light');
  await search.toggleFilter('Boards', 'Portrait sittings');
  await search.applyFilters();
  await search.expectResults(['Morning by the window']);
});

test('given the filter dialog, keyboard focus wraps between its first and last action', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.openFilters();
  await search.expectDialogBoundaryFocus();
  await search.escapeFilters();
  await search.expectFilterReturnFocus();
});

test('given ten draft tags, an eleventh selection is rejected without changing existing choices', async ({ page }) => {
  const search = new SearchPage(page);
  await search.open();
  await search.openFilters();
  const tags = ['window light', 'soft light', 'negative space', 'high contrast', 'muted palette', 'backlight', 'blue hour', 'symmetry', 'motion blur', 'minimal'];
  for (const tag of tags) await search.toggleFilter('Tags', tag);
  await search.toggleFilter('Tags', 'architecturalgeometryanduninterruptedshadowstudies');
  await search.expectTagLimit();
  await search.expectSelection('Tags', 'architecturalgeometryanduninterruptedshadowstudies', false);
  for (const tag of tags) await search.expectSelection('Tags', tag, true);
  await search.clearDraft();
  await search.toggleFilter('Tags', 'soft light');
  await search.applyFilters();
  await search.expectResults(['Morning by the window', 'Mara Lindqvist', 'Studio sitting no. 4', 'Saoirse Quinn']);
});
