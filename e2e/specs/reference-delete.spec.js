import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';

async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(1);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await inspiration.open(); await inspiration.openReference('Reference 01');
  const detail = new ReferenceDetailPage(page); await detail.openDelete();
  return { detail, library: inspiration.library, item: inspiration.library.items[0] };
}
test('Cancel keeps a reference; explicit Delete returns to Inspiration with confirmation', async ({ page }) => {
  const { detail, library } = await setup(page); await detail.cancelDelete(); expect(library.items).toHaveLength(1);
  await detail.openDelete(); await detail.confirmDelete(); await detail.expectDeletionComplete(); expect(library.items).toHaveLength(0);
});
test('A failed deletion stays open and retries safely', async ({ page }) => {
  const { detail, library } = await setup(page); library.failures.deleteReference = 1;
  await detail.confirmDelete(); await detail.expectDeleteError('Deletion was not confirmed. Try again to check its status.'); expect(library.items).toHaveLength(1);
  await detail.confirmDelete(); await detail.expectDeletionComplete(); expect(library.items).toHaveLength(0);
});
test('Stale deletion requires review of the latest saved reference', async ({ page }) => {
  const { detail, item } = await setup(page); item.revision++; item.notes = 'Other tab changed this.';
  await detail.confirmDelete(); await detail.expectDeleteError('This reference changed. Review its latest saved details before deleting.'); await detail.expectDeleteDisabled();
  await detail.reviewDeletion(); await detail.confirmDelete(); await detail.expectDeletionComplete();
});
