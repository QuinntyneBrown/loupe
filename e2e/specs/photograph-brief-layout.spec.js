import { test, expect } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);

for (const viewport of viewports) {
  test(`L2-002/043/044/045/046: brief recovery and discard at ${viewport.width}x${viewport.height}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    await work.openPhotograph('Study 01');
    const detail = new PhotographDetailPage(page);
    await detail.editBrief();
    const draft = { intent: 'Try a silhouette', genre: 'Landscape', experience: 'Intermediate', requestedFeedback: 'Simplify the shapes' };
    await detail.fillBrief(draft);
    work.library.pause('updateBrief');
    work.library.failures.updateBrief = 1;
    await detail.saveBrief();
    await detail.expectBriefSaving();
    expect(work.library.calls.filter(operation => operation === 'updateBrief')).toHaveLength(1);
    work.library.release('updateBrief');
    await detail.expectBriefFailure();
    await detail.expectBriefDraft(draft);
    await detail.expectAccessibleBrief();
    await detail.fillBrief({ genre: 'a'.repeat(101) });
    await detail.expectBriefLimit(100);
    await detail.expectAccessibleBrief();
    await detail.fillBrief({ genre: 'Landscape' });
    work.library.photos[0].brief.intent = 'Changed elsewhere';
    work.library.photos[0].revision++;
    await detail.saveBrief();
    await detail.expectBriefConflict();
    await detail.reloadLatestBrief();
    await detail.expectLatestBrief({ intent: 'Changed elsewhere' });
    await detail.expectBriefReviewFocus();
    await detail.expectAccessibleBrief();
    await detail.cancelBrief();
    await detail.expectDiscardKeyboardAndLayout();
    await detail.keepEditing();
    await detail.expectBriefDraft(draft);
    await detail.saveBrief();
    await detail.expectBriefValues(draft);
  });
}

test('L2-045.1: notes conflict review receives keyboard focus', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.editNotes('My draft');
  work.library.photos[0].notes = 'Changed elsewhere';
  work.library.photos[0].revision++;
  await detail.saveNotes();
  await detail.expectNotesConflict();
  await detail.reloadLatestNotes();
  await detail.expectLatestNotes('Changed elsewhere');
  await detail.expectNotesReviewFocus();
});
