// Acceptance Test
// Traces to: L2-055, L2-043, L2-044, L2-045
// Description: Add a location from the grid: a name alone saves and appears as a
// card, field errors stay at their field with the typed values kept, and the dialog
// protects unsaved changes, contains focus, returns focus to its trigger, and sizes
// as the shared upload dialog does.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { LocationsPage } from '../page-objects/locations-page.js';

async function setup(page, count) {
  await new MyWorkPage(page).configureCollection(0);
  const locations = new LocationsPage(page);
  await locations.configure(count);
  await locations.open();
  return locations;
}

test('L2-055.1: a name-only location saves from the header and appears as a new card with a notice', async ({ page }) => {
  const locations = await setup(page, 3);
  await locations.expectCards(3);
  await locations.openAdd('header');
  await locations.fill({ Name: '  Kew Bridge foreshore  ' });
  await locations.save();
  await locations.expectClosed();
  await locations.expectNotice('“Kew Bridge foreshore” saved.');
  await locations.expectCards(4);
  await locations.expectCard('Kew Bridge foreshore', { cover: false, meta: 'No images · No scouting report' });
  await locations.expectOrder(['Kew Bridge foreshore', 'Location 01', 'Location 02', 'Location 03']);
  const create = locations.library.calls.find((call) => call.operation === 'create');
  expect(create.name).toBe('  Kew Bridge foreshore  ');
  expect(create.coordinates).toBeNull();
  expect(create.setting).toBeNull();
  expect(create.tags).toEqual([]);
  expect(create.operationKey).toMatch(/^[!-~]{1,128}$/);
});

test('L2-043.2 / L2-055.1: the empty state offers Add location and the saved card replaces it', async ({ page }) => {
  const locations = await setup(page, 0);
  await locations.expectEmpty();
  await locations.openAdd('empty');
  await locations.fill({
    Name: 'Cornmill Gardens bandstand',
    'Address line 1': 'Cornmill Gardens',
    Locality: 'Lewisham',
    Latitude: '51.4612',
    Longitude: '-0.0115',
    Setting: 'Outdoor',
    'Scouting brief': 'Couple sessions under the bandstand roof.',
    Notes: 'Park on Loampit Vale.',
  });
  await locations.addTag('bandstand');
  await locations.save();
  await locations.expectClosed();
  await locations.expectCount(1);
  await locations.expectCard('Cornmill Gardens bandstand', { cover: false, meta: 'Lewisham · No images · No scouting report' });
  const create = locations.library.calls.find((call) => call.operation === 'create');
  expect(create.coordinates).toEqual({ latitude: '51.4612', longitude: '-0.0115' });
  expect(create.setting).toBe('Outdoor');
  expect(create.tags).toEqual([{ name: 'bandstand', category: null }]);
});

test('L2-055.3: field errors show at their field and keep the typed values', async ({ page }) => {
  const locations = await setup(page, 1);
  await locations.openAdd();
  await locations.fill({ Locality: 'Richmond' });
  await locations.save();
  await locations.expectFieldError('Name', 'Enter a name.');
  await locations.expectValue('Locality', 'Richmond');
  expect(locations.library.calls.filter((call) => call.operation === 'create')).toHaveLength(0);
  await locations.fill({ Name: 'Thames Path', Latitude: '51.487213' });
  await locations.save();
  await locations.expectNoFieldError('Name');
  await locations.expectFieldError('Longitude', 'Enter a longitude as well, or clear the latitude.');
  await locations.expectValue('Latitude', '51.487213');
  await locations.fill({ Longitude: '-0.287604' });
  locations.library.errors.create.push({ error: 'invalid_request', errors: { name: ['Use 200 characters or fewer.'] } });
  await locations.save();
  await locations.expectFieldError('Name', 'Use 200 characters or fewer.');
  await locations.expectValue('Name', 'Thames Path');
  await locations.expectValue('Longitude', '-0.287604');
  locations.library.failures.create = 1;
  await locations.save();
  await locations.expectSaveFailure();
  await locations.expectValue('Name', 'Thames Path');
  await locations.retrySave();
  await locations.expectClosed();
  await locations.expectCards(2);
});

for (const dismissal of ['Escape', 'backdrop', 'Close', 'Cancel', 'browser Back']) {
  test(`L2-055.8 / L2-043.7: a dirty Add location dialog protects its draft on ${dismissal}`, async ({ page }) => {
    const locations = await setup(page, 2);
    if (dismissal === 'browser Back') await locations.visitInspirationThenReturn();
    await locations.openAdd();
    await locations.fill({ Name: 'Draft place', Notes: 'Ask at the gate.' });
    const dismiss = () =>
      dismissal === 'Escape'
        ? locations.escape()
        : dismissal === 'backdrop'
          ? locations.clickBackdrop()
          : dismissal === 'Close'
            ? locations.close()
            : dismissal === 'Cancel'
              ? locations.cancel()
              : locations.navigateAway();
    await dismiss();
    await locations.expectDiscardChoice();
    await locations.keepEditing();
    if (dismissal === 'browser Back') await locations.expectStayed();
    await locations.expectValue('Name', 'Draft place');
    await locations.expectValue('Notes', 'Ask at the gate.');
    await dismiss();
    await locations.expectDiscardChoice();
    await locations.discardChanges();
    if (dismissal === 'browser Back') await locations.expectLeft();
    else {
      await locations.expectClosed();
      await locations.expectCards(2);
      await locations.openAdd();
      await locations.expectValue('Name', '');
      await locations.expectValue('Notes', '');
    }
  });
}

test('L2-045.2: a clean dialog closes without a prompt, contains focus, and returns focus to its trigger', async ({ page }) => {
  const locations = await setup(page, 0);
  await locations.openAdd('empty');
  await expect(locations.field('Name')).toBeFocused();
  await locations.expectFocusContained();
  await locations.escape();
  await locations.expectNoDiscardChoice();
  await locations.expectClosed();
  await locations.expectTriggerFocused('empty');
  await locations.openAdd('header');
  await locations.close();
  await locations.expectClosed();
  await locations.expectTriggerFocused('header');
  await locations.expectAccessible();
});

for (const viewport of [{ width: 375, height: 667 }, { width: 844, height: 390 }, { width: 1440, height: 900 }]) {
  test(`L2-044.6: the Add location dialog fits at ${viewport.width}x${viewport.height}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    const locations = await setup(page, 3);
    await locations.openAdd();
    await locations.expectDialogFits();
    await locations.expectAccessible();
  });
}
