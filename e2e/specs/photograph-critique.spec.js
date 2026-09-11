import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';

for (const mode of ['Live']) {
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

// Given an archived sample, opening the photograph offers real analysis without
// submitting work or presenting the sample as a completed critique.
test('L2-036.4: an archived sample is excluded from readiness and comparison', async ({ page }) => {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  work.library.photos[0].hasArchivedDemoCritique = true;
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph('Study 01');
  const detail = new PhotographDetailPage(page);
  await detail.expectArchivedSample();
  await detail.expectNoCritique();
  await detail.expectSaved();
  work.library.expectReadsOnly();
});
