// Acceptance tests. Traces to L2-056.4, L2-055.4, L2-055.5, L2-043, L2-044, L2-045.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { VideosPage, VideoDialog, VideoPlayerPage } from '../page-objects/videos-page.js';

async function setup(page, count = 3) {
  await new MyWorkPage(page).configureCollection(0);
  const videos = new VideosPage(page);
  await videos.configure(count);
  return { videos, player: new VideoPlayerPage(page), dialog: new VideoDialog(page) };
}

for (const width of [1440, 375])
  test(`L2-056.4: a card opens an in-app player with the video's details at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const { videos, player } = await setup(page);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    await videos.openFromNavigation();
    await videos.play('Video 01');
    await player.expectPlayer('Video 01', 'video000001');
    await player.expectDetails({ topic: 'Posing', channel: 'Studio Notes', tags: ['portrait', 'hands'], notes: 'Rewatch the hands section.' });
    await player.expectYouTubeLink('https://www.youtube.com/watch?v=video000001');
    await player.expectAccessible();
    await player.back();
    await videos.expectCards(3);
  });

test('L2-056.4: a saved player URL opens directly after sign-in and shows the summary and empty notes', async ({ page }) => {
  const { player } = await setup(page);
  await player.open('video-2');
  await player.expectPlayer('Video 02', 'video000002');
  await player.expectDetails({ topic: 'Lighting', summary: 'One light, many looks.', tags: ['portrait'], notes: null });
});

test('L2-055.4/L2-055.5: the player page edits and deletes the video', async ({ page }) => {
  const { videos, player, dialog } = await setup(page);
  await player.open('video-3');
  await player.expectPlayer('Video 03', 'video000003');
  await player.edit();
  await dialog.fill({ title: 'Interview with a legend', topic: 'Interview' });
  await dialog.save();
  await dialog.expectClosed();
  await player.expectPlayer('Interview with a legend', 'video000003');
  await player.expectNotice('“Interview with a legend” updated');
  await player.edit();
  await dialog.requestDelete();
  await dialog.confirmDelete('Interview with a legend');
  await expect(page).toHaveURL(/\/videos$/);
  await player.expectNotice('“Interview with a legend” deleted');
  await videos.expectCards(2);
});

test('L2-043: an unavailable or failed video load says so and recovers', async ({ page }) => {
  const { videos, player } = await setup(page, 1);
  videos.library.failures = 1;
  await player.open('video-1');
  await player.expectLoadFailure();
  await player.retry();
  await player.expectPlayer('Video 01', 'video000001');
  await player.open('video-9');
  await player.expectUnavailable();
  await player.expectAccessible();
});
