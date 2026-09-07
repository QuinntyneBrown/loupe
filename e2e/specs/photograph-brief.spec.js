import { test } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

for (const [field, maximum] of [['intent', 2000], ['genre', 100], ['requestedFeedback', 2000]]) {
  test(`L2-002.2: ${field} accepts its boundary and rejects one scalar above`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    await work.openPhotograph('Study 01');
    const detail = new PhotographDetailPage(page);
    await detail.editBrief();
    const value = '📷'.repeat(maximum);
    await detail.fillBrief({ [field]: value });
    await detail.saveBrief();
    await detail.expectBriefValues({ [field]: value });
    await detail.editBrief();
    await detail.fillBrief({ [field]: value + 'x' });
    await detail.expectBriefLimit(maximum);
  });
}

test('L2-002.1: brief editing preserves an unsaved note and both can be saved', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.editNotes('My unsaved note');
  await detail.editBrief();
  await detail.fillBrief({ intent: '  Deliberate motion blur  ', genre: ' Street ', experience: 'Professional', requestedFeedback: '  Look at the framing  ' });
  await detail.saveBrief();
  await detail.expectBriefValues({ intent: 'Deliberate motion blur', genre: 'Street', experience: 'Professional', requestedFeedback: 'Look at the framing' });
  await detail.expectNotes('My unsaved note');
  await detail.saveNotes();
  await detail.expectNotesState('Saved');
  await page.reload();
  await signIn.continue();
  await detail.expectBriefValues({ intent: 'Deliberate motion blur', genre: 'Street' });
  await detail.expectNotes('My unsaved note');
});

test('L2-002.4: all optional brief fields can be cleared', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.editBrief();
  await detail.fillBrief({ intent: '', genre: '', experience: '', requestedFeedback: '' });
  await detail.saveBrief();
  await detail.expectNoBrief();
  await page.reload();
  await signIn.continue();
  await detail.expectNoBrief();
});

test('L2-002.2/L2-043: brief errors retain values and cancellation discards only the edit', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.editBrief();
  const draft = { intent: 'Try a silhouette', genre: 'Landscape', experience: 'Intermediate', requestedFeedback: 'Simplify shapes' };
  await detail.fillBrief(draft);
  work.library.failures.updateBrief = 1;
  await detail.saveBrief();
  await detail.expectBriefFailure();
  await detail.expectBriefDraft(draft);
  await detail.retryBrief();
  await detail.expectBriefValues(draft);
  await detail.editBrief();
  await detail.fillBrief({ intent: '📷'.repeat(2001) });
  await detail.expectBriefLimit();
  await detail.cancelBrief();
  await detail.expectBriefValues(draft);
});
