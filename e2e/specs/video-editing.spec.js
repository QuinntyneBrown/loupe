// Acceptance tests. Traces to L2-055.1 through L2-055.5, L2-043, L2-045.
import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { VideosPage, VideoDialog } from '../page-objects/videos-page.js';

async function setup(page, count) {
  await new MyWorkPage(page).configureCollection(0);
  const videos = new VideosPage(page);
  await videos.configure(count);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await videos.openFromNavigation();
  return { videos, dialog: new VideoDialog(page) };
}

test('L2-055.1/L2-055.2: a video is saved with its topic and tags after client validation, then appears first', async ({ page }) => {
  const { videos, dialog } = await setup(page, 3);
  await videos.expectCards(3);
  await dialog.openAdd();
  await dialog.fill({ url: 'https://vimeo.com/12345', title: 'Posing hands' });
  await dialog.save();
  await dialog.expectAlert('Enter a YouTube video URL');
  expect(videos.library.calls.filter((call) => call.operation === 'save')).toHaveLength(0);
  await dialog.fill({ url: 'https://youtu.be/dQw4w9WgXcQ?t=42', topic: 'Posing', channel: 'Studio Notes', summary: 'Where hands go.', notes: 'Watch twice.' });
  await dialog.addTag('hands');
  await dialog.addTag('hands');
  await dialog.expectAlert('already included');
  await dialog.addTag('couples');
  await dialog.removeTag('couples');
  await dialog.save();
  await dialog.expectClosed();
  await dialog.expectNotice('“Posing hands” saved');
  await videos.expectCards(4);
  await videos.expectCount(4);
  await videos.expectCard('Posing hands', 'Posing', ['hands']);
  await videos.expectIndexing('Posing hands');
  const saved = videos.library.calls.find((call) => call.operation === 'save');
  expect(saved).toMatchObject({ title: 'Posing hands', url: 'https://youtu.be/dQw4w9WgXcQ?t=42', topic: 'posing', channel: 'Studio Notes', summary: 'Where hands go.', notes: 'Watch twice.', tags: [{ name: 'hands', category: null }] });
  await videos.expectAccessible();
});

test('L2-055.3: saving an already saved video explains the conflict and keeps the form', async ({ page }) => {
  const { videos, dialog } = await setup(page, 2);
  await dialog.openAdd();
  await dialog.fill({ url: videos.library.items[1].url, title: 'Again', topic: 'Lighting' });
  await dialog.save();
  await dialog.expectAlert('already saved');
  await dialog.expectValues({ title: 'Again' });
  await videos.expectCards(2);
});

test('L2-043: a failed save keeps the details and can be retried', async ({ page }) => {
  const { videos, dialog } = await setup(page, 1);
  await dialog.openAdd();
  await dialog.fill({ url: 'https://www.youtube.com/watch?v=abcdefghijk', title: 'Retry me', topic: 'Gear' });
  videos.library.failures = 1;
  await dialog.save();
  await dialog.expectAlert("Couldn't save this video");
  await dialog.expectValues({ title: 'Retry me', url: 'https://www.youtube.com/watch?v=abcdefghijk' });
  await dialog.retry();
  await dialog.expectClosed();
  await videos.expectCard('Retry me', 'Gear');
});

test('L2-055.4: editing replaces fields and tags, and a stale revision asks to review the latest', async ({ page }) => {
  const { videos, dialog } = await setup(page, 3);
  await dialog.openEdit('Video 02');
  await dialog.expectValues({ url: 'https://www.youtube.com/watch?v=video000002', title: 'Video 02', topic: 'lighting' });
  await dialog.expectTags(['portrait']);
  await dialog.fill({ title: 'One light portraits', topic: 'Interview' });
  await dialog.removeTag('portrait');
  await dialog.addTag('rembrandt');
  videos.library.items[1].revision = 2;
  await dialog.save();
  await dialog.expectAlert('This video changed');
  await dialog.reviewLatest();
  await dialog.save();
  await dialog.expectClosed();
  await videos.expectCard('One light portraits', 'Interview', ['rembrandt']);
  await videos.expectCards(3);
  const update = videos.library.calls.filter((call) => call.operation === 'update').at(-1);
  expect(update).toMatchObject({ id: 'video-2', revision: 2, title: 'One light portraits', topic: 'interview', tags: [{ name: 'rembrandt', category: null }] });
});

test('L2-055.5: deleting a video asks for confirmation and removes its card', async ({ page }) => {
  const { videos, dialog } = await setup(page, 3);
  await dialog.openEdit('Video 03');
  await dialog.requestDelete();
  await dialog.confirmDelete('Video 03');
  await dialog.expectClosed();
  await dialog.expectNotice('“Video 03” deleted');
  await videos.expectCards(2);
  await videos.expectCount(2);
  expect(videos.library.calls.filter((call) => call.operation === 'delete')).toEqual([{ operation: 'delete', id: 'video-3', revision: 1 }]);
});

test('L2-045: leaving with unsaved video details asks first and keeps the form on Keep editing', async ({ page }) => {
  const { videos, dialog } = await setup(page, 1);
  await dialog.openAdd();
  await dialog.fill({ url: 'https://youtu.be/dQw4w9WgXcQ', title: 'Unsaved' });
  await page.goBack();
  await dialog.keepEditing();
  await expect(page).toHaveURL(/\/videos$/);
  await dialog.expectValues({ title: 'Unsaved' });
  await page.goBack();
  await dialog.discard();
  await expect(page).toHaveURL(/\/my-work$/);
  expect(videos.library.calls.filter((call) => call.operation === 'save')).toHaveLength(0);
});
