import { test } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

test('L2-004.1/.4: save normalized notes, revisit and clear them', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.editNotes('  Step closer\nWatch the edges  ');
  await detail.expectNotesState('Unsaved changes');
  await detail.saveNotes();
  await detail.expectNotesState('Saved');
  await page.reload();
  await signIn.continue();
  await detail.expectNotes('Step closer\nWatch the edges');
  await detail.editNotes('');
  await detail.saveNotes();
  await detail.expectNotesState('Saved');
  await page.reload();
  await signIn.continue();
  await detail.expectNotes('');
});

test('L2-004.3: saving waits for acknowledgment and failed saves retain the draft for retry', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.editNotes('Try a lower viewpoint');
  work.library.pause('updateNotes');
  work.library.failures.updateNotes = 1;
  await detail.saveNotes();
  await detail.expectNotesState('Saving…');
  work.library.release('updateNotes');
  await detail.expectNotesFailure();
  await detail.expectNotes('Try a lower viewpoint');
  await detail.retryNotes();
  await detail.expectNotesState('Saved');
});

test('L2-004.4: the notes limit counts Unicode scalars and keeps oversized input editable', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  const maximum = '📷'.repeat(10000);
  await detail.editNotes(maximum);
  await detail.saveNotes();
  await detail.expectNotesState('Saved');
  await detail.editNotes(maximum + 'x');
  await detail.expectNotesLimit();
  await detail.expectNotes(maximum + 'x');
});
