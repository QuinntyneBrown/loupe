import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';

for (const mode of ['Live', 'Demo']) {
  test(`L2-006.1/L2-008.4/L2-036.1: revisit a complete ${mode} critique with its original brief and provenance`, async ({ page }) => {
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    work.library.critiques.set(work.library.photos[0].id, savedCritique(mode));
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    await work.openPhotograph('Study 01');
    const detail = new PhotographDetailPage(page);
    await detail.expectCritique(mode);
    await page.reload();
    await signIn.continue();
    await detail.expectCritique(mode);
    await detail.expectSaved();
    work.library.expectReadsOnly();
  });
}

test('L2-003.2: a photograph without a critique has an honest empty state', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  await new PhotographDetailPage(page).expectNoCritique();
  work.library.expectReadsOnly();
});

test('L2-003.4: critique loading and retry preserve the saved photograph context', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  work.library.critiques.set(work.library.photos[0].id, savedCritique());
  work.library.pause('getCritique');
  work.library.failures.getCritique = 1;
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectCritiqueLoading();
  await detail.expectSaved();
  work.library.release('getCritique');
  await detail.expectCritiqueFailure();
  await detail.retryCritique();
  await detail.expectCritique();
  await detail.expectCritiqueFocus();
  await detail.expectSaved();
  work.library.expectReadsOnly();
});
