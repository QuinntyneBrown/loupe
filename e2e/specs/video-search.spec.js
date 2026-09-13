// Acceptance tests. Traces to L2-056.2, L2-056.3, L2-058.1, L2-058.2, L2-058.3, L2-044, L2-045.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { VideosPage } from '../page-objects/videos-page.js';

async function setup(page, count = 7) {
  await new MyWorkPage(page).configureCollection(0);
  const videos = new VideosPage(page);
  await videos.configure(count);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await videos.open();
  await videos.expectCards(count);
  return videos;
}

test('L2-056.2/L2-056.3: keyword search composes with topic and tag filters and clears in one step', async ({ page }) => {
  const videos = await setup(page);
  await videos.search('one light');
  await videos.expectCards(2);
  await videos.expectMatching(2);
  await videos.expectCard('Video 02', 'Lighting');
  await videos.chooseTopic('Lighting');
  await videos.expectCards(2);
  await videos.chooseTopic('Posing');
  await videos.expectNoMatches();
  await videos.search('');
  await videos.expectCards(3);
  await videos.toggleTag('hands');
  await videos.expectTagPressed('hands', true);
  await videos.expectCards(3);
  await videos.chooseTopic('All');
  await videos.expectCards(3);
  await videos.expectCard('Video 07', 'Posing', ['hands']);
  const last = videos.library.calls.filter((call) => call.operation === 'list').at(-1);
  expect(last).toMatchObject({ mode: 'keyword', tags: ['hands'] });
  expect(last.topic ?? null).toBeNull();
  await videos.clearFilters();
  await videos.expectTagPressed('hands', false);
  await videos.expectCards(7);
  await videos.expectCount(7);
  await videos.expectAccessible();
});

test('L2-058.1/L2-058.2: meaning search ranks indexed videos with scores and asks for a query when blank', async ({ page }) => {
  const videos = await setup(page);
  await videos.chooseMode('Meaning');
  await videos.search('   ');
  await videos.expectQueryRequired();
  expect(videos.library.calls.filter((call) => call.operation === 'list' && call.mode === 'meaning')).toHaveLength(0);
  await videos.search('one light for couples');
  await videos.expectCards(6);
  await videos.expectScore('Video 05', '91% match');
  await videos.expectScore('Video 01', '34% match');
  await expect(page.getByRole('article').first().getByRole('heading')).toHaveText('Video 05');
  await expect(page.getByRole('heading', { name: 'Video 02', exact: true })).toHaveCount(0);
  await videos.chooseTopic('Lighting');
  await videos.expectCards(1);
  const last = videos.library.calls.filter((call) => call.operation === 'list').at(-1);
  expect(last).toMatchObject({ mode: 'meaning', query: 'one light for couples', topic: 'lighting' });
  await videos.chooseMode('Keyword');
  await videos.expectNoMatches();
  await videos.search('one light');
  await videos.expectCards(2);
  await videos.expectNoScores();
});

test('L2-058.3: unavailable meaning search says so and offers Keyword', async ({ page }) => {
  const videos = await setup(page, 3);
  videos.library.meaningError = 'integration_not_configured';
  await videos.chooseMode('Meaning');
  await videos.search('hands');
  await videos.expectMeaningUnavailable();
  await videos.expectCards(0);
  await videos.useKeyword();
  await videos.expectCards(1);
  await videos.expectCard('Video 01', 'Posing', ['hands']);
  await videos.expectNoScores();
  expect(videos.library.calls.filter((call) => call.operation === 'list').at(-1)).toMatchObject({ mode: 'keyword', query: 'hands' });
  await videos.expectAccessible();
});

test('L2-044/L2-045: the search controls fit and stay accessible at 375px', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 900 });
  const videos = await setup(page, 3);
  await videos.chooseMode('Meaning');
  await videos.search('light');
  await videos.expectCards(2);
  await videos.expectAccessible();
});
