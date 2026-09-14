// Acceptance Test
// Traces to: L2-060, L2-058, L2-057, L2-055, L2-041, L2-045
// Description: Request a scouting report from the location detail with its provider
// disclosure, watch it move through Queued and Running without waiting, read the six
// sections in order with ratings, evidence basis and cited images, and see Outdated,
// failed, not-configured, brief-changed and no-image states with their actions.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { LocationPage } from '../page-objects/location-page.js';

async function setup(page, count = 6) {
  await new MyWorkPage(page).configureCollection(0);
  const detail = new LocationPage(page);
  await detail.configure(count);
  return detail;
}

test('L2-060.1 / L2-041.2 / L2-045.3: requesting a report shows the disclosure, acknowledges without waiting, keeps editing available, and blocks a duplicate request', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.expectReportText('No scouting report yet');
  await detail.expectRequestAvailable();
  await detail.expectDisclosure();
  await detail.requestReport();
  await detail.expectProcessing('Queued for scouting');
  await detail.expectNoRequestControl();
  await detail.expectReportStatus('Queued');
  await detail.expectNotesEditable();
  expect(detail.library.scouting.calls.filter((call) => call.operation === 'request')).toHaveLength(1);
  const request = detail.library.scouting.calls.find((call) => call.operation === 'request');
  expect(request.operationKey).toMatch(/^[!-~]{1,128}$/);
  expect(request.regenerate).toBe(false);
  detail.library.scouting.advance(item, 'Running');
  await detail.expectProcessing('Looking at the images');
  detail.library.scouting.complete(item);
  await detail.expectReportSections(['Overview', 'Suitability', 'Time of day', 'Techniques', 'Group size', 'Cautions']);
  await detail.expectReportStatus('Report ready');
  await detail.expectNoRequestControl();
});

test('L2-058.1 / L2-058.7 / L2-060.8: a ready report renders every section in order with pills, evidence basis, cited images, provenance, and no scores', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  detail.library.scouting.ready(item);
  await detail.open(item.id);
  await detail.expectReportPill('AI generated');
  await detail.expectReportProvenance('Generated 10 Sep 2026 · Azure OpenAI · gpt-4.1 · 3 images · brief as saved');
  await detail.expectReportSections(['Overview', 'Suitability', 'Time of day', 'Techniques', 'Group size', 'Cautions']);
  await detail.expectEntry('Overview', 'Repeating arches give depth', { basis: 'Visible' });
  await detail.expectEntry('Suitability', 'Family portraits', { rating: 'Not recommended', basis: 'Visible' });
  await detail.expectEntry('Suitability', 'Events', { rating: 'Workable' });
  await detail.expectEntry('Time of day', 'Golden hour', { rating: 'Recommended', basis: 'Visible' });
  await detail.expectEntry('Time of day', 'Dawn', { rating: 'Unknown', basis: 'Inferred' });
  await detail.expectEntry('Techniques', 'Natural framing', { basis: 'Visible' });
  await detail.expectEntry('Group size', '2–8 people', { basis: 'Visible' });
  await detail.expectEntry('Cautions', 'Wet stones near the waterline are slippery.', { basis: 'Visible' });
  await detail.expectNoScores();
  await detail.citeImage('Overview', 'Repeating arches give depth', 3);
  await detail.expectSelected(3, false);
  await detail.expectStageUncropped('Location 03', 3);
  await detail.expectRegenerateAvailable();
  await detail.expectAccessible();
});

test('L2-060.3 / L2-060.4: an outdated report shows the count and date it used, offers Regenerate, and stays visible while regenerating', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  detail.library.scouting.ready(item, { imageCount: 2, status: 'Outdated' });
  await detail.open(item.id);
  await detail.expectReportStatus('Outdated');
  await detail.expectReportPill('Outdated');
  await detail.expectReportText('Based on 2 images as of 10 Sep 2026. The image set has changed since.');
  await detail.regenerateReport();
  await detail.expectProcessing('Regenerating with the current 3 images');
  await detail.expectReportPill('Previous report');
  await detail.expectReportSections(['Overview', 'Suitability', 'Time of day', 'Techniques', 'Group size', 'Cautions']);
  const request = detail.library.scouting.calls.find((call) => call.operation === 'request');
  expect(request.regenerate).toBe(true);
  detail.library.scouting.complete(item, { generatedAt: '2026-09-12T12:01:00Z' });
  await detail.expectReportProvenance('Generated 12 Sep 2026');
  await detail.expectReportStatus('Report ready');
});

test('L2-060.6: a failed job shows a safe reason with Retry and the earlier report stays readable', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  detail.library.scouting.ready(item);
  detail.library.scouting.fail(item);
  await detail.open(item.id);
  await detail.expectReportStatus('Failed');
  await detail.expectReportFailure('The analysis service could not complete this request.');
  await detail.expectReportPill('Previous report');
  await detail.expectReportSections(['Overview', 'Suitability', 'Time of day', 'Techniques', 'Group size', 'Cautions']);
  await detail.retryReport();
  await detail.expectProcessing('Regenerating with the current 3 images');
  await detail.expectReportPill('Previous report');
  expect(detail.library.scouting.calls.find((call) => call.operation === 'retry')).toBeTruthy();
});

test('L2-060.7: a missing integration is explained and the location stays editable', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  detail.library.scouting.errors.request.push({ error: 'integration_not_configured' });
  await detail.open(item.id);
  await detail.requestReport();
  await detail.expectReportText('Integration not configured. Set the provider credentials on the server to request scouting reports. You can keep editing this location.');
  await detail.expectNotesEditable();
  await detail.editText('notes', 'Still editing.');
  await detail.saveText('notes');
  await detail.expectTextStatus('notes', 'Saved');
});

test('L2-057.3 / L2-060.2: a location without images explains that an image is needed and offers no request', async ({ page }) => {
  const detail = await setup(page);
  await detail.open(detail.library.items[3].id);
  await detail.expectReportText('Add an image to get a scouting report.');
  await detail.expectNoRequestControl();
});

test('L2-055.5: a changed brief keeps the report current, shows the snapshot it used, and offers Regenerate', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  detail.library.scouting.ready(item, { briefSnapshot: 'An earlier brief.' });
  await detail.open(item.id);
  await detail.expectReportStatus('Report ready');
  await detail.expectReportText('This report used the brief as saved');
  await detail.expectReportText('An earlier brief.');
  await detail.expectRegenerateAvailable();
});
