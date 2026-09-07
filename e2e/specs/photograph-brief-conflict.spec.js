import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

async function openEditor(page) {
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await new MyWorkPage(page).openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.editBrief();
  return detail;
}

test('L2-030.3: a stale brief stays editable while its latest saved values are reviewed', async ({ page, context }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const first = await openEditor(page);
  const secondPage = await context.newPage();
  await work.library.attach(secondPage);
  const second = await openEditor(secondPage);
  await first.fillBrief({ intent: 'First editor intention' });
  await second.fillBrief({ intent: 'Second editor intention' });
  await first.saveBrief();
  await first.expectBriefValues({ intent: 'First editor intention' });
  await second.saveBrief();
  await second.expectBriefConflict();
  await second.expectBriefDraft({ intent: 'Second editor intention' });
  await second.reloadLatestBrief();
  await second.expectLatestBrief({ intent: 'First editor intention' });
  await second.expectBriefDraft({ intent: 'Second editor intention' });
  expect(work.library.photos[0].brief.intent).toBe('First editor intention');
  await second.saveBrief();
  await second.expectBriefValues({ intent: 'Second editor intention' });
  expect(work.library.photos[0].brief.intent).toBe('Second editor intention');
});

test('L2-030.3/L2-043: a failed brief reload retains the draft and can be retried', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const detail = await openEditor(page);
  await detail.fillBrief({ intent: 'Keep my intention' });
  work.library.photos[0].brief.intent = 'Changed elsewhere';
  work.library.photos[0].revision++;
  await detail.saveBrief();
  await detail.expectBriefConflict();
  work.library.failures.get = 1;
  await detail.reloadLatestBrief();
  await detail.expectBriefReloadFailure();
  await detail.expectBriefDraft({ intent: 'Keep my intention' });
  await detail.reloadLatestBrief();
  await detail.expectLatestBrief({ intent: 'Changed elsewhere' });
  await detail.expectBriefDraft({ intent: 'Keep my intention' });
});

test('L2-002.1/L2-004.1: saving notes preserves an open brief and allows it to save', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const detail = await openEditor(page);
  await detail.fillBrief({ intent: 'Keep my intention' });
  await detail.editNotes('Save this note first');
  await detail.saveNotes();
  await detail.expectNotesState('Saved');
  await detail.expectBriefDraft({ intent: 'Keep my intention' });
  await detail.saveBrief();
  await detail.expectBriefValues({ intent: 'Keep my intention' });
  await detail.expectNotes('Save this note first');
});
