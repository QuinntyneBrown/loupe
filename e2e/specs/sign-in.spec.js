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

test('L2-037.2: a failed identity callback offers a safe retry', async ({ page }) => {
  const signIn = new SignInPage(page);
  await signIn.openFailedCallback();
  await signIn.expectRetryableFailure();
  await signIn.continue();
  await new MyWorkPage(page).expectOpen();
});

for (const destination of ['https://attacker.example', '//attacker.example', '/\\attacker.example', '/%2f%2fattacker.example']) {
  test(`L2-037.1: unsafe destination ${destination} falls back to My Work`, async ({ page }) => {
    const signIn = new SignInPage(page);
    await signIn.openWithDestination(destination);
    await signIn.continue();
    await new MyWorkPage(page).expectOpen();
  });
}
