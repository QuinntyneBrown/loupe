import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';

test('L2-037.1: a signed-out visitor signs in and returns to the private destination', async ({ page }) => {
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.expectSignInRequired();
  await signIn.continue();
  const myWork = new MyWorkPage(page);
  await myWork.expectOpen();
  await myWork.signOut();
  await signIn.expectSignedOut();
  await page.goBack();
  await signIn.expectSignedOut();
});
