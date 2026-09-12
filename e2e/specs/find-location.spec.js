// Acceptance Test
// Traces to: L2-061, L2-062, L2-043, L2-044, L2-045
// Description: Find a location by keyword from the Locations area: the initial page
// explains the modes and filters, results show each location's cover or placeholder,
// name, locality, Recommended periods, group range, and the rating for every selected
// shoot type, report-less locations are labeled, the URL restores query, mode, and
// filters on reload and Back/Forward, No results and a service error are distinct
// states, and the filters move into a dialog on narrow viewports.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { FindLocationPage } from '../page-objects/find-location-page.js';

const shootTypes = ['Portraits', 'Family portraits', 'Headshots', 'Engagement', 'Events'];
const periods = ['Dawn', 'Morning', 'Midday', 'Afternoon', 'Golden hour', 'Blue hour', 'Night'];
const emptyRequest = { operation: 'search', mode: 'keyword', shootTypes: [], people: null, timesOfDay: [], setting: null, tags: [] };

async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const finder = new FindLocationPage(page);
  await finder.configure();
  return finder;
}

test('L2-043.2 / L2-045.1 / L2-045.5: Find a location opens from the Locations header, explains the modes and filters without implying a failure, and an example runs a keyword search', async ({ page }) => {
  const finder = await setup(page);
  await finder.openFromLocations();
  await finder.expectTitle();
  await finder.expectInitial();
  await finder.expectMode('Keyword');
  await finder.expectChips('Shoot type', shootTypes);
  await finder.expectChips('Time of day', periods);
  await finder.expectPeople('');
  await finder.expectSetting('Any');
  await finder.expectFiltersTrigger('Tags', false);
  expect(finder.searchCount()).toBe(0);
  await finder.expectAccessible();
  await finder.useExample(0);
  await finder.expectQuery('Golden hour engagement session with leading lines for two people');
  await expect.poll(() => finder.searchCount()).toBe(1);
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: 'Golden hour engagement session with leading lines for two people' });
  await expect(page).toHaveURL(/\/locations\/find\?q=Golden%20hour/);
});

test('L2-061.1 / L2-061.5 / L2-062.1: keyword results show the cover or placeholder, name, locality, Recommended periods, group range, and the rating for each selected shoot type, label report-less locations, and open the detail', async ({ page }) => {
  const finder = await setup(page);
  await finder.open();
  await finder.search('low tide');
  await finder.expectResultsHead('2 locations for “low tide”');
  await finder.expectCards(['Kew Bridge foreshore', 'Deptford creek stairs']);
  await finder.expectCard('Kew Bridge foreshore', { place: 'Richmond · 4 images', recommended: 'Morning, Golden hour', group: '2–8 people' });
  await finder.expectCard('Deptford creek stairs', { place: 'Lewisham · 1 image', noReport: true });
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: 'low tide' });
  await finder.toggleChip('Shoot type', 'Engagement');
  await finder.toggleChip('Shoot type', 'Events');
  await finder.expectChip('Shoot type', 'Engagement', true);
  await finder.expectChip('Shoot type', 'Events', true);
  await finder.expectResultsHead('1 location for “low tide”');
  await finder.expectCard('Kew Bridge foreshore', { place: 'Richmond · 4 images', recommended: 'Morning, Golden hour', group: '2–8 people', ratings: ['Engagement · Well suited', 'Events · Workable'] });
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: 'low tide', shootTypes: ['Engagement', 'Events'] });
  await expect(page).toHaveURL(/shootTypes=Engagement&shootTypes=Events/);
  await finder.toggleChip('Shoot type', 'Engagement');
  await finder.toggleChip('Shoot type', 'Events');
  await finder.search('lot');
  await finder.expectCard('Empty lot', { cover: false, place: 'No images', noReport: true });
  await finder.search('bandstand');
  await finder.expectCard('Cornmill Gardens bandstand', { place: 'Lewisham · 3 images', recommended: 'Afternoon, Golden hour', group: 'Group size · Cannot assess' });
  await finder.search('studio');
  await finder.expectCard('Window-lit studio', { place: '2 images', recommended: 'Morning', group: '1–2 people' });
  await finder.expectAccessible();
  await finder.search('foreshore');
  await finder.openCard('Kew Bridge foreshore');
});

test('L2-061.3 / L2-061.4 / L2-061.6: filters combine with each other, a keyword search with filters and no query returns every matching location, and Clear all restores the whole library', async ({ page }) => {
  const finder = await setup(page);
  await finder.open();
  await finder.setPeople('6');
  await finder.expectResultsHead('6 locations');
  await finder.expectCards(['Kew Bridge foreshore', 'Walthamstow wetlands', 'Hampstead ponds path', 'Peckham multi-storey roof', 'St Dunstan in the East', 'Bermondsey wall']);
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: '', people: 6 });
  await finder.toggleChip('Time of day', 'Dawn');
  await finder.expectCards(['Walthamstow wetlands']);
  await finder.toggleChip('Time of day', 'Morning');
  await finder.expectCards(['Kew Bridge foreshore', 'Walthamstow wetlands', 'Hampstead ponds path']);
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: '', people: 6, timesOfDay: ['Dawn', 'Morning'] });
  await finder.chooseSetting('Outdoor');
  await finder.expectCards(['Kew Bridge foreshore', 'Walthamstow wetlands', 'Hampstead ponds path']);
  await finder.chooseSetting('Mixed');
  await finder.expectNoResults('');
  await finder.expectAccessible();
  await finder.clearAll();
  await finder.expectResultsHead('10 locations');
  await finder.expectPeople('');
  await finder.expectChip('Time of day', 'Dawn', false);
  await finder.expectSetting('Any');
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: '' });
});

test('L2-062.8: reload, Back and Forward restore the query, mode, and filters; No results offers editing the query or clearing filters; a service error is a separate retryable state', async ({ page }) => {
  const finder = await setup(page);
  await finder.open('?q=arches&shootTypes=Engagement&people=4&timesOfDay=Golden%20hour&setting=Outdoor&tags=river');
  const request = { ...emptyRequest, query: 'arches', shootTypes: ['Engagement'], people: 4, timesOfDay: ['Golden hour'], setting: 'Outdoor', tags: ['river'] };
  await finder.expectQuery('arches');
  await finder.expectMode('Keyword');
  await finder.expectChip('Shoot type', 'Engagement', true);
  await finder.expectPeople('4');
  await finder.expectChip('Time of day', 'Golden hour', true);
  await finder.expectSetting('Outdoor');
  await finder.expectFiltersTrigger('Tags', true);
  await finder.expectCards(['Kew Bridge foreshore']);
  expect(finder.lastSearch()).toEqual(request);
  await finder.reload();
  await finder.expectCards(['Kew Bridge foreshore']);
  await finder.expectQuery('arches');
  await finder.expectPeople('4');
  expect(finder.lastSearch()).toEqual(request);
  await finder.search('skyline');
  await finder.expectNoResults('skyline');
  await finder.back();
  await finder.expectQuery('arches');
  await finder.expectCards(['Kew Bridge foreshore']);
  await finder.forward();
  await finder.expectQuery('skyline');
  await finder.expectNoResults('skyline');
  await finder.clearEmptyFilters();
  await finder.expectCards(['Peckham multi-storey roof']);
  await finder.expectChip('Shoot type', 'Engagement', false);
  await finder.expectPeople('');
  await finder.expectSetting('Any');
  await finder.expectFiltersTrigger('Tags', false);
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: 'skyline' });
  await finder.search('nowhere');
  await finder.expectNoResults('nowhere');
  await finder.expectNoEmptyClear();
  await finder.editQuery();
  await finder.expectQueryFocused();
  finder.library.failures.search = 1;
  await finder.search('roof');
  await finder.expectError();
  await finder.expectAccessible();
  await finder.retry();
  await finder.expectCards(['Peckham multi-storey roof']);
  await finder.chooseMode('Meaning');
  await expect(page).toHaveURL(/mode=meaning/);
  expect(finder.lastSearch().mode).toBe('meaning');
  await finder.reload();
  await finder.expectMode('Meaning');
  await finder.expectQuery('roof');
});

test('L2-045.2 / L2-045.3: the filters dialog lists the library tags with counts, keeps focus inside, closes on Escape restoring focus to its trigger, limits tags to ten, and recovers from a tag load failure', async ({ page }) => {
  const finder = await setup(page);
  await finder.open();
  await finder.openFilters('Tags');
  const choices = [['arches', 2], ['river', 2], ['bandstand', 1], ['brick', 1], ['low tide', 1], ['open sky', 1], ['path', 1], ['reeds', 1], ['rooftop', 1], ['ruin', 1], ['skyline', 1], ['stairs', 1], ['water', 1], ['window light', 1]];
  await finder.expectTagChoices(choices);
  await finder.expectDialogFocusTrapped();
  for (const [name] of choices.slice(0, 11)) await finder.toggleDialogChip('Tags', name);
  await finder.expectDialogError('Choose up to 10 tags.');
  await finder.expectDialogChip('Tags', 'skyline', false);
  await finder.expectAccessible();
  await finder.escapeFilters();
  await finder.expectFocused('Tags');
  expect(finder.searchCount()).toBe(0);
  finder.library.failures.tags = 1;
  await finder.openFilters('Tags');
  await finder.expectTagsFailed();
  await finder.expectTagChoices(choices);
  await finder.toggleDialogChip('Tags', 'river');
  await finder.toggleDialogChip('Tags', 'arches');
  await finder.applyFilters();
  await finder.expectFocused('Tags');
  await finder.expectFiltersTrigger('Tags', true);
  await finder.expectCards(['Kew Bridge foreshore']);
  expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: '', tags: ['river', 'arches'] });
  await expect(page).toHaveURL(/tags=river&tags=arches/);
});

for (const [width, columns] of [
  [320, 1],
  [375, 1],
  [575, 1],
  [576, 2],
  [767, 2],
  [768, 3],
  [991, 3],
  [992, 3],
  [1199, 3],
  [1200, 4],
  [1440, 4],
]) {
  test(`L2-044.1 / L2-045.5 / L2-045.6: at ${width}px the results use ${columns} column${columns === 1 ? '' : 's'}, the filters ${width < 768 ? 'move into a dialog' : 'stay in the page'}, and the page is accessible`, async ({ page }, info) => {
    await page.setViewportSize({ width, height: 900 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const finder = await setup(page);
    await finder.open('?q=');
    await finder.expectResultsHead('10 locations');
    await finder.expectGridColumns(columns);
    await finder.expectAccessible();
    await finder.capture(info.outputPath(`find-location-results-${width}.png`));
    if (width < 768) {
      await finder.expectNoDesktopFilters();
      await finder.expectFiltersTrigger('Filters', false);
      await finder.openFilters('Filters');
      await finder.toggleDialogChip('Shoot type', 'Engagement');
      await finder.fillDialogPeople('2');
      await finder.toggleDialogChip('Time of day', 'Golden hour');
      await finder.chooseDialogSetting('Outdoor');
      await finder.toggleDialogChip('Tags', 'river');
      await finder.expectDialogReachable();
      await finder.expectAccessible();
      await finder.capture(info.outputPath(`find-location-filters-${width}.png`));
      await finder.applyFilters();
      await finder.expectCards(['Kew Bridge foreshore']);
      await finder.expectFiltersTrigger('5 filters', true);
      expect(finder.lastSearch()).toEqual({ ...emptyRequest, query: '', shootTypes: ['Engagement'], people: 2, timesOfDay: ['Golden hour'], setting: 'Outdoor', tags: ['river'] });
    } else {
      await finder.expectDesktopFilters();
      await finder.openFilters('Tags');
      await finder.expectDialogReachable();
      await finder.expectAccessible();
      await finder.capture(info.outputPath(`find-location-filters-${width}.png`));
      await finder.escapeFilters();
    }
  });
}
