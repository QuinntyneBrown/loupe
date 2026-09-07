import { test } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { DeletionPage } from '../page-objects/deletion-page.js';

// Given each shared viewport, when deletion is canceled, pending, recovered or
// reviewed, then keyboard focus, targets, long text and cleanup status remain usable.
const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);

for (const viewport of viewports) {
  test(`L2-031/043/044/045/046: deletion recovery at ${viewport.width}x${viewport.height}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    await work.openPhotograph('Study 01');
    const detail = new PhotographDetailPage(page);
    await detail.expectSaved();
    await detail.deletePhotograph();
    await detail.expectDeletionKeyboard();
    await detail.expectAccessibleDeletion();
    await detail.escapeDeletion();
    await detail.expectDeletionCancelled();
    work.library.pause('deletePhotograph');
    work.library.failures.deletePhotograph = 1;
    await detail.deletePhotograph();
    await detail.confirmDeletion();
    await detail.expectDeleting();
    await detail.expectPendingDeletionKeyboard();
    await detail.expectAccessibleDeletion();
    work.library.release('deletePhotograph');
    await detail.expectDeletionFailure();
    await detail.expectUnloadProtection(false);
    await detail.expectAccessibleDeletion();
    work.library.photos[0].revision++;
    work.library.photos[0].notes = 'Saved observation. '.repeat(500);
    work.library.photos[0].brief.intent = 'A'.repeat(2000);
    await detail.retryDeletion();
    await detail.expectDeletionConflict();
    await detail.reviewDeletion();
    await detail.expectDeletionReviewFocus();
    await detail.expectAccessibleDeletion();
    await detail.expectReviewActionsReachable();
    await detail.confirmDeletion();
    const deletion = new DeletionPage(page);
    await deletion.expectPending();
    await deletion.expectAccessibleStatus();
    work.library.failures.getDeletion = 1;
    await deletion.checkStatus();
    await deletion.expectFailure();
    await deletion.expectAccessibleStatus();
    work.library.completeDeletion();
    await deletion.checkStatus();
    await deletion.expectCompleted();
    await deletion.expectAccessibleStatus();
    await deletion.backToMyWork();
    await work.expectEmpty();
  });
}
