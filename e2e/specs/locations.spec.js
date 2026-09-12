// Acceptance Test
// Traces to: L2-057, L2-043, L2-044, L2-045
// Description: The Locations area is the fifth main area: it opens by navigation or
// URL, lists every owned location once in the shared order with its cover or
// placeholder, name, locality, image count and report status, pages, recovers from
// failure, offers Add location when empty, and follows the image-grid column rule.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { LocationsPage } from '../page-objects/locations-page.js';

async function setup(page, count) {
  await new MyWorkPage(page).configureCollection(0);
  const locations = new LocationsPage(page);
  await locations.configure(count);
  return locations;
}

test('L2-043.1: Locations opens from the fifth navigation link and directly by URL with its title and active state', async ({ page }) => {
  const locations = await setup(page, 3);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await expect(locations.navigation().getByRole('link')).toHaveCount(5);
  await locations.navigateFromLibrary();
  await locations.expectActiveNavigation();
  await locations.expectCards(3);
  await locations.reload();
  await locations.expectActiveNavigation();
  await locations.expectCards(3);
});

test('L2-057.1: locations page once each in the shared order with cover or placeholder, name, locality, image count, and report status', async ({ page }) => {
  const locations = await setup(page, 25);
  const release = locations.library.hold('list');
  await locations.open();
  await locations.expectLoading(8);
  release();
  await locations.expectCount(25);
  await locations.expectCards(24);
  await locations.expectCard('Location 01', { cover: true, meta: 'Richmond · 1 image · No scouting report' });
  await locations.expectCard('Location 04', { cover: false, meta: 'No images · No scouting report' });
  await locations.expectCard('Location 05', { cover: true, meta: 'Richmond · 1 image', status: 'Report' });
  await locations.expectCard('Location 06', { cover: true, meta: '2 images · No scouting report' });
  await locations.expectOrder(locations.library.items.slice(0, 24).map((item) => item.name));
  await locations.more();
  await locations.expectCards(25);
  await locations.expectCardFocused('Location 25');
  expect(locations.library.calls.map((call) => call.operation)).toEqual(['list', 'list']);
});

test('L2-057.4 / L2-043.2: an empty library offers Add location and failures recover without losing loaded cards', async ({ page }) => {
  const locations = await setup(page, 25);
  locations.library.failures.list = 1;
  await locations.open();
  await locations.expectError();
  await locations.retry();
  await locations.expectCards(24);
  locations.library.failures.list = 1;
  await locations.more();
  await locations.expectError();
  await locations.expectCards(24);
  await locations.retry();
  await locations.expectCards(25);
  locations.library.items = [];
  await locations.reload();
  await locations.expectEmpty();
  await locations.expectAccessible();
});

test('L2-057.4: a failed empty library recovers to the empty state with focus on its heading', async ({ page }) => {
  const locations = await setup(page, 0);
  locations.library.failures.list = 1;
  await locations.open();
  await locations.expectError();
  await locations.retry();
  await locations.expectEmpty();
  await locations.expectEmptyFocused();
});

for (const width of [575, 576, 767, 768, 991, 992, 1199, 1200]) {
  test(`L2-044.1: the locations grid uses the image-grid column rule at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const locations = await setup(page, 25);
    await locations.open();
    await locations.expectCards(24);
    await locations.expectGridColumns(width < 576 ? 1 : width < 768 ? 2 : width < 992 ? 3 : width < 1200 ? 4 : 5);
    await locations.expectAccessible();
  });
}

for (const width of [320, 1440]) {
  test(`L2-045.1 / L2-045.6: the locations grid is accessible with reachable targets at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const locations = await setup(page, 25);
    await locations.open();
    await locations.expectCards(24);
    await locations.expectTargets();
    await locations.expectAccessible();
  });
}
