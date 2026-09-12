import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { LocationLibrary } from "../fixtures/location-library.js";
import { SignInPage } from "./sign-in-page.js";

export class LocationsPage {
  constructor(page) {
    this.page = page;
  }
  async configure(count) {
    this.library = new LocationLibrary(count);
    await this.library.attach(this.page);
  }
  async open() {
    await this.page.goto("/locations");
    await new SignInPage(this.page).continue();
  }
  async reload() {
    await this.page.reload();
    await new SignInPage(this.page).continue();
  }
  navigation() {
    return this.page.getByRole("navigation", { name: "Library", exact: true });
  }
  async navigateFromLibrary() {
    await this.navigation()
      .getByRole("link", { name: "Locations", exact: true })
      .click();
  }
  async expectActiveNavigation() {
    await expect(this.page).toHaveURL(/\/locations$/);
    await expect(this.page).toHaveTitle("Locations · Loupe");
    await expect(
      this.navigation().getByRole("link", { name: "Locations", exact: true }),
    ).toHaveAttribute("aria-current", "page");
    await expect(
      this.page.getByRole("heading", { level: 1, name: "Locations" }),
    ).toBeVisible();
  }
  async expectCount(count) {
    await expect(
      this.page.getByText(`${count} locations`, { exact: true }),
    ).toBeVisible();
  }
  cards() {
    return this.page.getByRole("article");
  }
  async expectCards(count) {
    await expect(this.cards()).toHaveCount(count);
  }
  card(name) {
    return this.cards().filter({
      has: this.page.getByRole("heading", { name, exact: true }),
    });
  }
  async expectCard(name, { cover, meta, status }) {
    const card = this.card(name);
    await expect(
      card.getByRole("link", { name: `Open ${name}`, exact: true }),
    ).toHaveAttribute("href", /\/locations\//);
    if (cover) await expect(card.getByRole("img", { name })).toBeVisible();
    else
      await expect(
        card.getByRole("img", { name: "No images yet", exact: true }),
      ).toBeVisible();
    await expect(card.getByText(meta, { exact: true })).toBeVisible();
    if (status)
      await expect(card.getByText(status, { exact: true })).toBeVisible();
  }
  async expectOrder(names) {
    await expect(this.cards().getByRole("heading")).toHaveText(names);
  }
  async expectLoading(count) {
    await expect(this.page.locator(".lp-skeleton--tile")).toHaveCount(count);
    await expect(this.page.getByRole("region", { name: "Your locations" })).toHaveAttribute("aria-busy", "true");
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
  async expectEmpty() {
    await expect(
      this.page.getByRole("heading", {
        name: "Keep the places you've scouted.",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("button", { name: "Add location", exact: true }),
    ).toHaveCount(2);
  }
  async expectEmptyFocused() {
    await expect(
      this.page.getByRole("heading", {
        name: "Keep the places you've scouted.",
        exact: true,
      }),
    ).toBeFocused();
  }
  async expectError() {
    await expect(
      this.page.getByRole("alert").getByRole("heading", {
        name: "Couldn't load your locations.",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("heading", {
        name: "Keep the places you've scouted.",
        exact: true,
      }),
    ).toHaveCount(0);
  }
  async expectGridColumns(columns) {
    const cards = this.cards();
    const firstTop = (await cards.first().boundingBox()).y;
    let firstRow = 0;
    for (let index = 0; index < Math.min(6, await cards.count()); index++) {
      if (Math.abs((await cards.nth(index).boundingBox()).y - firstTop) < 2)
        firstRow++;
    }
    expect(firstRow).toBe(columns);
  }
  async expectTargets() {
    for (const control of [
      this.page.getByRole("button", { name: "Add location", exact: true }),
      this.page.getByRole("button", { name: "Load more", exact: true }),
      this.navigation().getByRole("link", { name: "Locations", exact: true }),
    ]) {
      const box = await control.boundingBox();
      expect(box.width).toBeGreaterThanOrEqual(24);
      expect(box.height).toBeGreaterThanOrEqual(24);
    }
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
