// Acceptance Test
// Traces to: L2-037, L2-043, L2-044, L2-045, L2-046
import { test } from "@playwright/test";
import { SignInPage } from "../page-objects/sign-in-page.js";
import { MyWorkPage } from "../page-objects/my-work-page.js";

test("L2-037.1: a signed-out visitor signs in and returns to the private destination", async ({
  page,
}) => {
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.expectSignInRequired();
  await signIn.continue();
  const myWork = new MyWorkPage(page);
  await myWork.expectOpen();
  await myWork.expectContentFocus();
  await myWork.signOut();
  await signIn.expectSignedOut();
  await signIn.expectContentFocus();
  await page.goBack();
  await signIn.expectSignedOut();
});

test("L2-037.2: invalid credentials offer a safe retry", async ({ page }) => {
  const signIn = new SignInPage(page);
  await signIn.openFailedSignIn();
  await signIn.expectRetryableFailure();
  await signIn.continue();
  await new MyWorkPage(page).expectOpen();
});

test("L2-043: session service failure offers retry without a blank private page", async ({
  page,
}) => {
  const signIn = new SignInPage(page);
  await signIn.makeSessionUnavailable();
  await signIn.openPrivateDestination();
  await signIn.expectSessionRetry();
  await signIn.retrySession();
  await signIn.expectSignInRequired();
  await signIn.continue();
  await new MyWorkPage(page).expectOpen();
});

for (const destination of [
  "https://attacker.example",
  "//attacker.example",
  "/\\attacker.example",
  "/%2f%2fattacker.example",
]) {
  test(`L2-037.1: unsafe destination ${destination} falls back to My Work`, async ({
    page,
  }) => {
    const signIn = new SignInPage(page);
    await signIn.openWithDestination(destination);
    await signIn.continue();
    await new MyWorkPage(page).expectOpen();
  });
}

test("L2-037.12: failed sign-in retains email and clears the password", async ({
  page,
}) => {
  const signIn = new SignInPage(page);
  await signIn.openFailedSignIn();
  await signIn.expectRetryableFailure();
  await signIn.expectCredentialsRetainedSafely("photographer@example.com");
});
test("L2-037.12: sign-in service failure allows a successful retry", async ({
  page,
}) => {
  const signIn = new SignInPage(page);
  await signIn.makeSignInUnavailable();
  await signIn.openPrivateDestination();
  await signIn.continue();
  await signIn.expectUnavailable();
  await signIn.expectCredentialsRetainedSafely("photographer@example.com");
  await signIn.continue();
  await new MyWorkPage(page).expectOpen();
});
test("L2-037.12: invalid input shows field errors", async ({ page }) => {
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.signIn("invalid", "short");
  await signIn.expectFieldErrors();
});
