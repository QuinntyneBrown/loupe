import { test } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

async function openDetail(page) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  return { work, detail: new PhotographDetailPage(page) };
}

test('L2-043.5: navigation offers Keep editing and Discard for an unsaved note', async ({ page }) => {
  const { work, detail } = await openDetail(page);
  await detail.editNotes('Keep this unfinished thought');
  await detail.returnToLibrary();
  await detail.expectDiscardChoice();
  await detail.keepEditing();
  await detail.expectNotes('Keep this unfinished thought');
  await detail.returnToLibrary();
  await detail.expectDiscardChoice();
  await detail.discardChanges();
  await work.expectOpen();
  await work.openPhotograph('Study 01');
  await detail.expectNotes('Keep the edges quiet.\nTry a lower viewpoint.');
});

test('L2-043.5: closing a dirty brief confirms only that edit and preserves an unsaved note', async ({ page }) => {
  const { detail } = await openDetail(page);
  await detail.editNotes('Separate unfinished note');
  await detail.editBrief();
  await detail.fillBrief({ intent: 'Unfinished intention' });
  await detail.cancelBrief();
  await detail.expectDiscardChoice();
  await detail.escapeDiscard();
  await detail.expectBriefDraft({ intent: 'Unfinished intention' });
  await detail.cancelBrief();
  await detail.expectDiscardChoice();
  await detail.discardChanges();
  await detail.expectBriefValues({ intent: 'Explore quiet morning light' });
  await detail.expectNotes('Separate unfinished note');
});

test('L2-043.5: leaving with an unsaved brief retains it until Discard is chosen', async ({ page }) => {
  const { work, detail } = await openDetail(page);
  await detail.editBrief();
  await detail.fillBrief({ genre: 'Street' });
  await detail.returnToLibrary();
  await detail.expectDiscardChoice();
  await detail.keepEditing();
  await detail.expectBriefDraft({ genre: 'Street' });
  await detail.returnToLibrary();
  await detail.discardChanges();
  await work.expectOpen();
});

test('L2-043.5: browser unload protection applies only while edits are unsaved', async ({ page }) => {
  const { detail } = await openDetail(page);
  await detail.expectSaved();
  await detail.expectUnloadProtection(false);
  await detail.editNotes('Unfinished note');
  await detail.expectUnloadProtection(true);
  await detail.saveNotes();
  await detail.expectNotesState('Saved');
  await detail.expectUnloadProtection(false);
  await detail.editBrief();
  await detail.fillBrief({ genre: 'Street' });
  await detail.expectUnloadProtection(true);
  await detail.saveBrief();
  await detail.expectBriefValues({ genre: 'Street' });
  await detail.expectUnloadProtection(false);
});

test('L2-043.5: an unchanged brief closes without a discard prompt', async ({ page }) => {
  const { work, detail } = await openDetail(page);
  await detail.editBrief();
  await detail.cancelBrief();
  await detail.expectBriefValues({ intent: 'Explore quiet morning light' });
  await detail.expectNoDiscardChoice();
  await detail.returnToLibrary();
  await work.expectOpen();
});
