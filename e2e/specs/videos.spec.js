// Acceptance tests. Traces to L2-056.1, L2-043, L2-044, L2-045.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { VideosPage } from '../page-objects/videos-page.js';

async function setup(page, count) {
  await new MyWorkPage(page).configureCollection(0);
  const videos = new VideosPage(page);
  await videos.configure(count);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  return videos;
}

test('L2-056.1: saved videos show thumbnails, topics, tags and safe YouTube links, and page without losing cards on retry', async ({ page }) => {
  const videos = await setup(page, 25);
  await videos.openFromNavigation();
  await videos.expectCurrentNavigation();
  await videos.expectCount(25);
  await videos.expectCards(24);
  await videos.expectCard('Video 01', 'Posing', ['portrait', 'hands']);
  await videos.expectCard('Video 02', 'Lighting', ['portrait']);
  await videos.expectIndexing('Video 02');
  await videos.expectNoIndexing('Video 01');
  videos.library.failures = 1;
  await videos.more();
  await videos.expectError();
  await videos.expectCards(24);
  await videos.retry();
  await videos.expectCards(25);
  await videos.expectCardFocused('Video 25');
  expect(videos.library.calls.filter((call) => call.operation === 'list')).toHaveLength(3);
});

test('L2-043: empty and failed video collections recover in place', async ({ page }) => {
  const videos = await setup(page, 0);
  videos.library.failures = 1;
  await videos.open();
  await videos.expectError();
  await videos.retry();
  await videos.expectEmpty();
  await videos.expectEmptyFocused();
  await videos.expectAccessible();
});

for (const width of [1440, 768, 375])
  test(`L2-044/L2-045: video cards are accessible without overflow at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const videos = await setup(page, 7);
    await videos.open();
    await videos.expectCards(7);
    await videos.expectAccessible();
  });
