import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

export class SignInPage {
  constructor(page) {
    this.page = page;
  }
  async openPrivateDestination() {
    await this.page.goto("/my-work");
  }
  async expectSignInRequired() {
    await expect(
      this.page.getByRole("heading", {
        name: "A private space for your photography",
      }),
    ).toBeVisible();
    await expect(this.page).toHaveURL(/\/sign-in\?returnUrl=%2Fmy-work$/);
  }
  async continue() {
    await this.signIn("photographer@example.com", "local acceptance password");
  }
  async signIn(email, password) {
    await this.page.getByLabel("Email", { exact: true }).fill(email);
    await this.page.getByLabel("Password", { exact: true }).fill(password);
    await this.page
      .getByRole("button", { name: "Sign in", exact: true })
      .click();
  }
  async expectCredentialsRetainedSafely(email) {
    await expect(this.page.getByLabel("Email", { exact: true })).toHaveValue(
      email,
    );
    await expect(this.page.getByLabel("Password", { exact: true })).toHaveValue(
      "",
    );
  }
  async makeSignInUnavailable() {
    await this.page.addInitScript(() => {
      window.loupeFixture = { signInUnavailable: true };
    });
  }
  async expectUnavailable() {
    await expect(this.page.getByRole("alert")).toHaveText(
      "Sign-in is unavailable. Please try again.",
    );
  }
  async expectFieldErrors() {
    await expect(
      this.page.getByText("Enter a valid email address.", { exact: true }),
    ).toBeVisible();
    await expect(
      this.page.getByText("Use a password with 15 to 128 characters.", {
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectSignedOut() {
    await expect(
      this.page.getByRole("heading", {
        name: "A private space for your photography",
      }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("button", { name: "Sign out", exact: true }),
    ).toHaveCount(0);
  }
  async openFailedSignIn() {
    await this.page.goto("/sign-in");
    await this.signIn("photographer@example.com", "incorrect long password");
  }
  async expectRetryableFailure() {
    await expect(this.page.getByRole("alert")).toHaveText(
      "Email or password is incorrect.",
    );
    await expect(
      this.page.getByRole("button", { name: "Sign in", exact: true }),
    ).toBeEnabled();
  }
  async openWithDestination(destination) {
    await this.page.goto(
      `/sign-in?returnUrl=${encodeURIComponent(destination)}`,
    );
  }
  async expectContentFocus() {
    await expect(this.page.getByRole("main")).toBeFocused();
  }
  async expectAccessibleLayout() {
    const audit = await new AxeBuilder({ page: this.page })
      .withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"])
      .analyze();
    expect(audit.violations).toEqual([]);
    const fits = await this.page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    );
    expect(fits).toBe(true);
    const button = this.page.getByRole("button", {
      name: "Sign in",
      exact: true,
    });
    await button.scrollIntoViewIfNeeded();
    const box = await button.boundingBox();
    expect(box?.width).toBeGreaterThanOrEqual(24);
    expect(box?.height).toBeGreaterThanOrEqual(24);
  }
  async capture(path) {
    await this.page.screenshot({ path, fullPage: true });
  }
  async makeSessionUnavailable() {
    await this.page.addInitScript(() => {
      window.loupeFixture = { sessionUnavailable: true };
    });
  }
  async expectSessionRetry() {
    await expect(this.page.getByRole("alert")).toHaveText(
      "Your session could not be checked. Please try again.",
    );
    await expect(
      this.page.getByRole("button", { name: "Try again", exact: true }),
    ).toBeEnabled();
  }
  async retrySession() {
    await this.page
      .getByRole("button", { name: "Try again", exact: true })
      .click();
  }
  async expectFontsLoaded() {
    // document.fonts.check() is unreliable here — Chromium reports it true even for a
    // font name with no @font-face and no matching system font at all. document.fonts.load()
    // genuinely distinguishes "a matching @font-face exists and loaded" from "it doesn't".
    await this.page.evaluate(() => document.fonts.ready);
    for (const font of [
      '400 16px "Instrument Sans"',
      '600 16px "Instrument Sans"',
      '400 16px "Spline Sans Mono"',
    ]) {
      const loaded = await this.page.evaluate(
        (f) => document.fonts.load(f).then((list) => list.length),
        font,
      );
      expect(
        loaded,
        `expected a loaded @font-face for ${font}`,
      ).toBeGreaterThan(0);
    }
  }
}
