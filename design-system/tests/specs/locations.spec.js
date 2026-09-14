// Acceptance Test
// Traces to: L2-051, L2-052, L2-044, L2-045
// Description: The Locations examples show location cards with every report status,
// Find a location result cards with the collection column rule, a keyboard-operable
// gallery whose selection is announced, a scouting report with ratings, evidence
// basis and cited images, the search-index status pills with Retry, and the Meaning
// mode label — all synthetic, with no external request.
import { test } from '@playwright/test';
import { LocationsPage } from '../page-objects/locations-page.js';

test('L2-051.3 / L2-052.1: location cards, result cards, gallery, report, and status pills render every state', async ({ page }) => {
  const locations = new LocationsPage(page);
  await locations.openFromReference();
  await locations.expectReady();
  await locations.expectCards([
    ['Kew Bridge foreshore', 'Report ready'],
    ['Peckham multi-storey roof', 'Outdated'],
    ['Walthamstow wetlands', 'Queued'],
    ['St Dunstan in the East', 'Scouting'],
    ['Bermondsey wall', 'Failed'],
    ['Empty lot', 'No scouting report'],
  ]);
  await locations.expectPlaceholder('Empty lot');
  await locations.expectResult('Kew Bridge foreshore', ['Richmond · 4 images', 'Recommended · Morning, Golden hour', '2–8 people'], ['Engagement · Well suited', 'Events · Workable']);
  await locations.expectResult('Cornmill Gardens bandstand', ['Lewisham · 3 images', 'Group size · Cannot assess'], ['Engagement · Workable', 'Events · Well suited']);
  await locations.expectResult('Deptford creek stairs', ['Lewisham · 1 image'], ['No scouting report']);
  await locations.expectGallery(4);
  await locations.selectImage(3);
  await locations.expectStage(3);
  await locations.pressArrow('ArrowRight');
  await locations.expectStage(4);
  await locations.pressArrow('ArrowLeft');
  await locations.expectStage(3);
  await locations.setAsCover();
  await locations.expectCover(3);
  await locations.expectReportSections(['Overview', 'Suitability', 'Time of day', 'Techniques', 'Group size', 'Cautions']);
  await locations.expectEntry('Overview', 'Repeating arches give depth', { basis: 'Visible' });
  await locations.expectEntry('Suitability', 'Family portraits', { rating: 'Not recommended', basis: 'Visible' });
  await locations.expectEntry('Time of day', 'Dawn', { rating: 'Unknown', basis: 'Inferred' });
  await locations.expectEntry('Group size', '2–8 people', { basis: 'Visible' });
  await locations.expectNoScores();
  await locations.citeImage('Overview', 'Repeating arches give depth', 1);
  await locations.expectStage(1);
  await locations.expectStatusPills(['Processing report', 'Updating search', 'Search indexing failed · Retry']);
  await locations.retryIndexing();
  await locations.expectModes();
  await locations.toggleShootType('Engagement');
  await locations.expectNoAccessibilityViolations();
  await locations.expectNoExternalRequests();
});

for (const width of [375, 575, 576, 767, 768, 1199, 1200, 1440]) {
  test(`L2-044.1: the result collection uses the one/two/three/three/four column rule at ${width}`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const locations = new LocationsPage(page);
    await locations.open();
    await locations.expectResultColumns(width);
    await locations.expectFitsViewport();
  });
}
