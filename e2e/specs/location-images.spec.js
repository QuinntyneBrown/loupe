// Acceptance Test
// Traces to: L2-056, L2-057
// Description: Manage a location's images from its gallery: several files upload as
// separate operations with their own progress, retry and removal, capacity is
// enforced before any transfer, removal and cover changes take effect at once, a
// cancelled transfer leaves no partial image, and thumbnails are a keyboard radio group.
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

const png = (name) => ({ name, type: 'image/png' });

test('L2-056.3 / L2-056.1: several files upload as separate operations with their own progress and each saved image appears at once', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.expectGallery(3);
  await detail.openAddImages();
  await detail.chooseFiles([png('north.png'), png('south.png')]);
  const release = detail.library.hold('addImage');
  await detail.startUpload();
  await detail.expectUploadRow('north.png', 'Uploading');
  await detail.expectUploadRow('south.png', 'Uploading');
  const keys = detail.library.calls.filter((call) => call.operation === 'addImage').map((call) => call.operationKey);
  expect(new Set(keys).size).toBe(2);
  await detail.library.reportProgress(page, keys[0], 400, 1000);
  await detail.expectRowProgress('north.png', 400, 1000);
  release();
  await detail.expectUploadRow('north.png', 'Saved');
  await detail.expectUploadRow('south.png', 'Saved');
  await detail.expectGalleryCount(5);
  await detail.expectCapacity('5 of 10 images');
  await detail.finishUploads();
  await detail.selectImage(5);
  await detail.expectStageUncropped('Location 03', 5);
  expect(item.images.map((image) => image.position)).toEqual([1, 2, 3, 4, 5]);
  expect(item.coverImageId).toBe(item.images[0].id);
});

test('L2-056.3: a failed or invalid file affects only its own row and can be retried or removed', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.openAddImages();
  await detail.chooseFiles([png('one.png'), png('two.png'), { name: 'scan.tif', type: 'image/tiff' }]);
  detail.library.errors.addImage.push(null, { error: 'request_failed' });
  await detail.startUpload();
  await detail.expectUploadRow('one.png', 'Saved');
  await detail.expectUploadRow('two.png', "didn't upload");
  await detail.expectRowActions('two.png', ['Retry', 'Remove']);
  await detail.expectUploadRow('scan.tif', "a format Loupe can't read");
  await detail.expectRowActions('scan.tif', ['Remove']);
  await detail.expectGalleryCount(4);
  await detail.retryRow('two.png');
  await detail.expectUploadRow('two.png', 'Saved');
  await detail.expectGalleryCount(5);
  await detail.removeRow('scan.tif');
  await detail.expectNoRow('scan.tif');
  await detail.finishUploads();
  expect(item.images).toHaveLength(5);
  expect(detail.library.calls.filter((call) => call.operation === 'addImage' && call.filename === 'scan.tif')).toHaveLength(0);
});

test('L2-056.4 / L2-056.2: an over-capacity selection is rejected before any transfer and Add images is disabled at ten', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  item.images = Array.from({ length: 8 }, (_, index) => ({ ...item.images[0], id: `${item.id}-seed-${index + 1}`, position: index + 1 }));
  item.coverImageId = item.images[0].id;
  await detail.open(item.id);
  await detail.expectGalleryCount(8);
  await detail.openAddImages();
  await detail.chooseFiles([png('a.png'), png('b.png'), png('c.png')]);
  await detail.expectCapacityRejection('You chose 3 images, but only 2 more fit.');
  expect(detail.library.calls.filter((call) => call.operation === 'addImage')).toHaveLength(0);
  await detail.chooseFiles([png('a.png'), png('b.png')]);
  await detail.startUpload();
  await detail.expectUploadRow('a.png', 'Saved');
  await detail.expectUploadRow('b.png', 'Saved');
  await detail.finishUploads();
  await detail.expectGalleryCount(10);
  await detail.expectAddImagesDisabled('10 of 10 images. Remove one to add another.');
});

test('L2-056.5: removing an image updates the gallery at once and moves the cover to the next image', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.expectGallery(3);
  await detail.selectImage(2);
  await detail.removeSelected();
  await detail.expectRemoveDialog(2);
  await detail.cancelRemove();
  await detail.expectGalleryCount(3);
  await detail.removeSelected();
  await detail.expectRemoveDialog(2);
  await detail.confirmRemove();
  await detail.expectGalleryCount(2);
  await detail.expectSelected(1, true);
  await detail.expectCapacity('2 of 10 images');
  await detail.removeSelected();
  await detail.confirmRemove();
  await detail.expectGalleryCount(1);
  await detail.expectSelected(1, true);
  expect(item.images).toHaveLength(1);
  expect(item.coverImageId).toBe(item.images[0].id);
});

test('L2-056.6: a chosen cover persists after reload on the detail and the grid card', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.expectSetAsCoverDisabled();
  await detail.selectImage(3);
  await detail.setAsCover();
  await detail.expectSelected(3, true);
  await detail.reload();
  await detail.expectGalleryCount(3);
  await detail.selectImage(3);
  await detail.expectSelected(3, true);
  expect(item.coverImageId).toBe(item.images[2].id);
  expect(item.images.map((image) => image.position)).toEqual([1, 2, 3]);
  const locations = new LocationsPage(page);
  locations.library = detail.library;
  await locations.open();
  await locations.expectCard('Location 03', { cover: true, meta: 'Richmond · 3 images · No scouting report' });
});

test('L2-056.9: cancelling remaining uploads aborts the transfer and the notice offers Refresh', async ({ page }) => {
  const detail = await setup(page);
  const item = detail.library.items[2];
  await detail.open(item.id);
  await detail.openAddImages();
  await detail.chooseFiles([png('first.png'), png('second.png')]);
  const release = detail.library.hold('addImage');
  await detail.startUpload();
  await detail.expectUploadRow('second.png', 'Uploading');
  await detail.cancelRemaining();
  await expect(detail.imagesDialog()).toHaveCount(0);
  expect(detail.library.aborted.length).toBeGreaterThan(0);
  await detail.expectGalleryCount(3);
  await detail.expectUploadNotice('Uploads stopped.');
  release();
  await detail.refreshFromNotice();
  await detail.expectGalleryCount(5);
});

test('L2-057.5: thumbnails are a radio group operable by keyboard with the selection announced', async ({ page }) => {
  const detail = await setup(page);
  await detail.open(detail.library.items[2].id);
  await detail.expectGallery(3);
  await detail.focusThumbnail(1);
  await detail.pressKey('ArrowRight');
  await detail.expectSelected(2, false);
  await detail.expectStageUncropped('Location 03', 2);
  await detail.pressKey('ArrowRight');
  await detail.expectSelected(3, false);
  await detail.pressKey('ArrowLeft');
  await detail.expectSelected(2, false);
  await detail.expectAccessible();
});
