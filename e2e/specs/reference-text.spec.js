import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';

async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(1);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue(); await inspiration.open(); await inspiration.openReference('Reference 01');
  return { detail: new ReferenceDetailPage(page), library: inspiration.library, item: inspiration.library.items[0], signIn };
}

test('Description and personal notes save independently, persist and can be cleared', async ({ page }) => {
  const { detail, item, signIn } = await setup(page); const notes = item.notes;
  await detail.editText('description', 'Warm backlight and open space.'); await detail.expectUnload(true); await detail.saveText('description'); await detail.expectTextSaved('description');
  expect(item.notes).toBe(notes); expect(item.descriptionProvenance).toBe('manual');
  await detail.editText('notes', 'Try this with the north window.'); await detail.saveText('notes'); await detail.expectTextSaved('notes');
  await page.reload(); await signIn.continue(); await detail.expectText('description', 'Warm backlight and open space.'); await detail.expectText('notes', 'Try this with the north window.');
  await detail.editText('description', ''); await detail.saveText('description'); await detail.expectTextSaved('description'); expect(item.description).toBeNull();
});

test('Failed text saves preserve the draft, and stale revisions require explicit review', async ({ page }) => {
  const { detail, library, item } = await setup(page); library.failures.updateText = 1;
  await detail.editText('notes', 'Keep my draft'); await detail.saveText('notes'); await detail.expectTextError('notes', 'Your change could not be saved. It is still here. Try again.');
  await detail.expectText('notes', 'Keep my draft'); item.revision++; item.notes = 'Other tab';
  await detail.saveText('notes'); await detail.expectTextError('notes', 'This reference changed. Review the latest value before saving.');
  await detail.reviewText('notes'); await detail.expectText('notes', 'Keep my draft'); await detail.saveText('notes'); await detail.expectTextSaved('notes'); expect(item.notes).toBe('Keep my draft');
});
