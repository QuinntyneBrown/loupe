import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { PhotographerLibrary } from "../fixtures/photographer-library.js";
import { SignInPage } from "./sign-in-page.js";

export class PhotographersPage {
  constructor(page) {
    this.page = page;
  }
  async configure(count) {
    this.library = new PhotographerLibrary(count);
    await this.library.attach(this.page);
  }
  async open() {
    await this.page.goto("/photographers");
    await new SignInPage(this.page).continue();
  }
  async expectCount(count) {
    await expect(
      this.page.getByText(`${count} bookmarked`, { exact: true }),
    ).toBeVisible();
  }
  async expectCards(count) {
    await expect(this.page.getByRole("article")).toHaveCount(count);
  }
  async expectCard(name, domain, count) {
    const card = this.page
      .getByRole("article")
      .filter({ has: this.page.getByRole("heading", { name, exact: true }) });
    await expect(
      card.getByRole("link", { name: `Open ${name}`, exact: true }),
    ).toHaveAttribute("href", /\/photographers\//);
    const source = card.getByRole("link", {
      name: `Open ${domain}`,
      exact: true,
    });
    await expect(source).toHaveAttribute("target", "_blank");
    await expect(source).toHaveAttribute("rel", "noopener noreferrer");
    await expect(source).toHaveAttribute("referrerpolicy", "no-referrer");
    await expect(
      card.getByText(count ? `${count} references` : "No references yet", {
        exact: true,
      }),
    ).toBeVisible();
  }
  async more() {
    await this.page
      .getByRole("button", { name: "Load more", exact: true })
      .click();
  }
  async retry() {
    await this.page
      .getByRole("button", { name: "Try again", exact: true })
      .click();
  }
  async expectCardFocused(name) {
    await expect(
      this.page.getByRole("link", { name: `Open ${name}`, exact: true }),
    ).toBeFocused();
  }
  async expectEmptyFocused() {
    await expect(
      this.page.getByRole("heading", {
        name: "Bookmark the photographers you learn from.",
        exact: true,
      }),
    ).toBeFocused();
  }
  dialog() {
    return this.page.getByRole("dialog");
  }
  async expectPreviewActions() {
    await expect(
      this.dialog().getByRole("button", { name: "Back", exact: true }),
    ).toBeVisible();
    await expect(
      this.dialog().getByRole("button", { name: "Cancel", exact: true }),
    ).toHaveCount(0);
  }
  async expectValidationOnly() {
    await expect(this.dialog().getByRole("alert")).toContainText(
      "Enter a public HTTP",
    );
    await expect(
      this.dialog().getByRole("button", { name: "Try again", exact: true }),
    ).toHaveCount(0);
  }
  async openAdd() {
    await this.page
      .getByRole("button", { name: "Add photographer", exact: true })
      .first()
      .click();
  }
  async readPortfolio(url, name = "") {
    await this.dialog()
      .getByRole("textbox", { name: "Website", exact: true })
      .fill(url);
    if (name)
      await this.dialog().getByRole("textbox", { name: /^Name/ }).fill(name);
    await this.dialog()
      .getByRole("button", { name: "Read the page", exact: true })
      .click();
  }
  async expectReading() {
    await expect(
      this.dialog().getByRole("heading", {
        name: "Reading the page…",
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectPreview() {
    await expect(
      this.dialog().getByRole("textbox", { name: /^Description/ }),
    ).toBeVisible();
  }
  async editPreview(name, description, notes) {
    await this.dialog()
      .getByRole("textbox", { name: "Name", exact: true })
      .fill(name);
    await this.dialog()
      .getByRole("textbox", { name: /^Description/ })
      .fill(description);
    await this.dialog()
      .getByRole("textbox", { name: /^Your notes/ })
      .fill(notes);
  }
  async addTag(name) {
    const input = this.dialog().getByRole("textbox", { name: /^Tags/ });
    await input.fill(name);
    await input.press("Enter");
  }
  async removeTag(name) {
    await this.dialog()
      .getByRole("button", { name: `Remove tag ${name}`, exact: true })
      .click();
  }
  async saveAdd() {
    await this.dialog()
      .getByRole("button", { name: "Add photographer", exact: true })
      .click();
  }
  async cancelAdd() {
    await this.dialog()
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
    await expect(this.dialog()).not.toBeVisible();
  }
  async expectClosed() {
    await expect(this.dialog()).not.toBeVisible();
  }
  async expectFallback() {
    await expect(
      this.dialog().getByRole("heading", {
        name: "Couldn't read that site",
        exact: true,
      }),
    ).toBeVisible();
  }
  async manualFallback(name, notes) {
    await this.dialog()
      .getByRole("textbox", { name: "Name", exact: true })
      .fill(name);
    await this.dialog()
      .getByRole("textbox", { name: /^Your notes/ })
      .fill(notes);
  }
  async expectDuplicate() {
    await expect(
      this.dialog().getByRole("heading", {
        name: "Already bookmarked",
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectSaveFailure() {
    await expect(this.dialog().getByRole("alert")).toContainText(
      "Couldn't add this photographer",
    );
  }
  async retryAdd() {
    await this.dialog()
      .getByRole("button", { name: "Try again", exact: true })
      .click();
  }
  async expectError() {
    await expect(
      this.page.getByRole("heading", {
        name: "Couldn't load your photographers.",
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectEmpty() {
    await expect(
      this.page.getByRole("heading", {
        name: "Bookmark the photographers you learn from.",
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectAccessible() {
    expect(
      (
        await new AxeBuilder({ page: this.page })
          .withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"])
          .analyze()
      ).violations,
    ).toEqual([]);
    expect(
      await this.page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
  }
}
