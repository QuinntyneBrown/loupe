import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { SignInPage } from "./sign-in-page.js";
export class PhotographerPage {
  constructor(page) {
    this.page = page;
  }
  async open(id = "photographer-1") {
    await this.page.goto("/photographers/" + id);
    await new SignInPage(this.page).continue();
  }
  async expectProfile(name, summary, notes) {
    await expect(
      this.page.getByRole("heading", { name, exact: true, level: 1 }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("region", { name: "Summary", exact: true }),
    ).toContainText(summary);
    await expect(
      this.page.getByRole("textbox", { name: "Your notes", exact: true }),
    ).toHaveValue(notes);
  }
  async expectEmpty() {
    await expect(
      this.page.getByRole("heading", {
        name: "No references linked yet.",
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectReferences(count) {
    await expect(
      this.page
        .getByRole("region", { name: "References", exact: true })
        .getByRole("article"),
    ).toHaveCount(count);
  }
  async more() {
    await this.page
      .getByRole("button", { name: "Load more", exact: true })
      .click();
  }
  async expectError() {
    await expect(
      this.page.getByRole("heading", {
        name: "Couldn't load this photographer.",
        exact: true,
      }),
    ).toBeVisible();
  }
  async retry() {
    await this.page
      .getByRole("button", { name: "Try again", exact: true })
      .click();
  }
  async expectSource(url, current = true) {
    await expect(
      this.page.getByRole("link", {
        name: current ? "Captured page" : "Previous captured page",
        exact: true,
      }),
    ).toHaveAttribute("href", url);
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
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  }
}
