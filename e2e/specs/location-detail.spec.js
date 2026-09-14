// Acceptance Test
// Traces to: L2-055, L2-057, L2-062, L2-044, L2-045
// Description: Inspect a location by URL: the gallery, address, coordinates, setting,
// brief, notes, tags, timestamps and report status are shown; details are edited in
// a revision-protected dialog, the brief, notes and tags save inline, deletion is
// confirmed with its effects, and the layout stacks or sits side by side by width.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { LocationPage } from '../page-objects/location-page.js';
import { LocationsPage } from '../page-objects/locations-page.js';

async function setup(page, count = 6) {
  await new MyWorkPage(page).configureCollection(0);
  const detail = new LocationPage(page);
  await detail.configure(count);
  return detail;
}

test('L2-057.2 / L2-055.2: a location opened by URL shows every detail and its gallery, and survives reload', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.expectTitle('Location 03');
  await detail.expectAddress(['1 Riverside Walk', 'Richmond', 'Greater London', 'United Kingdom']);
  await detail.expectDetail('Coordinates', '51.487213, -0.287604');
  await detail.expectDetail('Created', '12 Sep 2026');
  await detail.expectDetail('Updated', '12 Sep 2026');
  await detail.expectMeta('Richmond, Greater London');
  await detail.expectSetting('Outdoor');
  await detail.expectText('scoutingBrief', 'Couple sessions at low tide, two people, nothing staged.');
  await detail.expectText('notes', "Parking on Kew Green, five minutes' walk.");
  await detail.expectTags(['river', 'low tide']);
  await detail.expectReportStatus('No scouting report yet');
  await detail.expectGallery(3);
  await detail.expectStageUncropped('Location 03', 1);
  await detail.selectImage(3);
  await detail.expectStageUncropped('Location 03', 3);
  await detail.expectCapacity('3 of 10 images');
  await detail.reload();
  await detail.expectTitle('Location 03');
  await detail.expectGallery(3);
  await detail.expectAccessible();
});

test('L2-057.3: a location without images shows a placeholder, Add images, and why there is no report', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[3];
  await detail.open(item.id);
  await detail.expectTitle('Location 04');
  await detail.expectNoImages();
  await detail.expectAddressAbsent();
  await detail.expectDetail('Coordinates', 'Not recorded');
  await detail.expectReportStatus('Add an image to get a scouting report.');
  await detail.expectAccessible();
});

test('L2-057.4: an unavailable or foreign location offers retry and a way back', async ({ page }) => {
  const detail = await setup(page);
  await detail.open('00000000-0000-4000-9000-000000000099');
  await detail.expectUnavailable();
  detail.library.failures.get = 1;
  await detail.open(detail.library.items[0].id);
  await detail.expectUnavailable();
  await detail.retry();
  await detail.expectTitle('Location 01');
  detail.library.failures.get = 1;
  await detail.reload();
  await detail.expectUnavailable();
  await detail.backToLocations();
});

test('L2-055.2 / L2-055.4 / L2-055.6: editing details updates the detail, leaves tags alone, and protects against stale saves', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[0];
  await detail.open(item.id);
  await detail.openEdit();
  await detail.expectField('Name', 'Location 01');
  await detail.expectField('Address line 1', '1 Riverside Walk');
  await detail.expectField('Latitude', '51.487213');
  await detail.expectNoField('Scouting brief');
  await detail.expectNoField('Notes');
  await detail.fill({ Name: 'Kew Bridge foreshore', 'Address line 1': 'Thames Path, north bank', 'Address line 2': '  ', Latitude: '51.48721', Longitude: '-0.2876', Setting: 'Mixed' });
  await detail.saveEdit();
  await detail.expectEditClosed();
  await detail.expectTitle('Kew Bridge foreshore');
  await detail.expectAddress(['Thames Path, north bank', 'Richmond', 'Greater London', 'United Kingdom']);
  await detail.expectDetail('Coordinates', '51.487210, -0.287600');
  await detail.expectSetting('Mixed');
  await detail.expectDetail('Updated', '13 Sep 2026');
  await detail.expectTags(['river', 'low tide']);
  await detail.expectMenuFocused();
  await detail.openEdit();
  await detail.fill({ Name: 'Kew Bridge foreshore, low tide' });
  item.revision += 1;
  await detail.saveEdit();
  await detail.expectEditConflict();
  await detail.expectField('Name', 'Kew Bridge foreshore, low tide');
  await detail.reloadLatest();
  await detail.expectField('Name', 'Kew Bridge foreshore, low tide');
  await detail.saveEdit();
  await detail.expectEditClosed();
  await detail.expectTitle('Kew Bridge foreshore, low tide');
  await detail.openEdit();
  await detail.fill({ Latitude: '91' });
  await detail.saveEdit();
  await detail.expectEditFieldError('Latitude', 'Enter decimal degrees from -90 to 90 with up to six decimal places.');
});

test('L2-055.8: a dirty Edit location dialog protects its draft', async ({ page }) => {
  const detail = await setup(page);
  await detail.open(detail.library.items[0].id);
  await detail.openEdit();
  await detail.fill({ Name: 'Draft rename' });
  await detail.escape();
  await detail.expectDiscardChoice();
  await detail.keepEditing();
  await detail.expectField('Name', 'Draft rename');
  await detail.escape();
  await detail.expectDiscardChoice();
  await detail.discardChanges();
  await detail.expectEditClosed();
  await detail.expectTitle('Location 01');
  await detail.expectMenuFocused();
});

test('L2-055.4 / L2-055.5: the brief, notes, and tags save inline with their own status and never touch each other', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[0];
  await detail.open(item.id);
  await detail.expectTextStatus('scoutingBrief', 'Saved');
  await detail.editText('scoutingBrief', 'Wide and quiet, not the bridge as a landmark.');
  await detail.expectTextStatus('scoutingBrief', 'Unsaved');
  await detail.saveText('scoutingBrief');
  await detail.expectTextStatus('scoutingBrief', 'Saved');
  await detail.editText('notes', 'Ask at the boatyard if the gate is shut.');
  detail.library.failures.updateText = 1;
  await detail.saveText('notes');
  await detail.expectTextStatus('notes', "Couldn't save");
  await detail.expectTextError('notes', 'Your change could not be saved. It is still here. Try again.');
  await detail.expectText('notes', 'Ask at the boatyard if the gate is shut.');
  await detail.saveText('notes');
  await detail.expectTextStatus('notes', 'Saved');
  await detail.expectTags(['river', 'low tide']);
  await detail.addTag('arches');
  await detail.removeTag('river');
  await detail.expectTagStatus('Unsaved');
  await detail.saveTags();
  await detail.expectTagStatus('Saved');
  await detail.expectTags(['low tide', 'arches']);
  await detail.expectText('scoutingBrief', 'Wide and quiet, not the bridge as a landmark.');
  expect(item.tags.map((tag) => tag.name)).toEqual(['low tide', 'arches']);
  expect(item.scoutingBrief).toBe('Wide and quiet, not the bridge as a landmark.');
  expect(item.notes).toBe('Ask at the boatyard if the gate is shut.');
});

test('L2-057.6: deleting a location names it, states its effects, and leads back to Locations', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.openDelete();
  await detail.expectDeleteDialog('Location 03', 'Its 3 images, scouting report, notes, tags, and search record are all removed.');
  await detail.cancelDelete();
  await detail.expectMenuFocused();
  await detail.expectTitle('Location 03');
  expect(detail.library.deletions).toEqual([]);
  await detail.openDelete();
  await detail.confirmDelete();
  await detail.expectDeleted();
  expect(detail.library.deletions).toEqual([item.id]);
  const locations = new LocationsPage(page);
  locations.library = detail.library;
  await locations.expectCards(5);
});

test('L2-057.6: a location can be deleted from its grid card', async ({ page }) => {
  await new MyWorkPage(page).configureCollection(0);
  const locations = new LocationsPage(page);
  await locations.configure(4);
  await locations.open();
  await locations.deleteCard('Location 02');
  const detail = new LocationPage(page);
  await detail.expectDeleteDialog('Location 02', 'Its 2 images, scouting report, notes, tags, and search record are all removed.');
  await detail.confirmDelete();
  await locations.expectNotice('Location deleted.');
  await locations.expectCards(3);
  await locations.expectOrder(['Location 01', 'Location 03', 'Location 04']);
});

for (const width of [991, 992]) {
  test(`L2-044.2: the gallery and details ${width < 992 ? 'stack' : 'sit side by side'} at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const detail = await setup(page);
    await detail.open(detail.library.items[2].id);
    await detail.expectGallery(3);
    await detail.expectLayout(width >= 992);
    await detail.expectAccessible();
  });
}

test('L2-045: the detail is accessible at phone width', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 667 });
  const detail = await setup(page);
  await detail.open(detail.library.items[2].id);
  await detail.expectGallery(3);
  await detail.expectAccessible();
});

test('L2-062.4 / L2-062.5 / L2-062.6: the detail reports Processing report, Updating search, and Search indexing failed with Retry, and shows nothing when current or not configured', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[0];
  detail.library.indexing(item, 'processing-report');
  await detail.open(item.id);
  await detail.expectIndexStatus('Processing report');
  detail.library.indexing(item, 'updating');
  await detail.expectIndexStatus('Updating search');
  detail.library.indexing(item, 'failed', 'index-op-1');
  await detail.expectIndexStatus('Search indexing failed');
  await detail.expectNotesEditable();
  await detail.editText('notes', 'Still editing while indexing failed.');
  await detail.saveText('notes');
  await detail.expectTextStatus('notes', 'Saved');
  await detail.retryIndexing();
  await detail.expectIndexStatus('Updating search');
  expect(detail.library.indexRetries).toEqual([
    expect.objectContaining({ operationId: 'index-op-1', revision: item.revision }),
  ]);
  expect(detail.library.indexRetries[0].operationKey).toMatch(/^[!-~]{1,128}$/);
  detail.library.indexing(item, 'current');
  await detail.expectNoIndexStatus();
  await detail.expectAccessible();
  detail.library.indexing(item, 'not-configured');
  await detail.reload();
  await detail.expectTitle(item.name);
  await detail.expectNoIndexStatus();
});
