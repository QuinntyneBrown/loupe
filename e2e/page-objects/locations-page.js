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
      this.page.getByText(`${count} ${count === 1 ? "location" : "locations"}`, { exact: true }),
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
  async deleteCard(name) {
    await this.card(name).hover();
    await this.card(name)
      .getByRole("button", { name: "Delete location", exact: true })
      .click();
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
  addTrigger(entry = "header") {
    return this.page
      .getByRole("button", { name: "Add location", exact: true })
      .nth(entry === "empty" ? 1 : 0);
  }
  async openAdd(entry = "header") {
    await this.addTrigger(entry).click();
    await expect(this.dialog()).toBeVisible();
  }
  dialog() {
    return this.page.getByRole("dialog", { name: "Add location", exact: true });
  }
  discardDialog() {
    return this.page.getByRole("dialog", {
      name: "Discard unsaved changes?",
      exact: true,
    });
  }
  field(label) {
    return this.dialog().getByLabel(new RegExp("^" + label + "( optional.*)?$"));
  }
  async fill(values) {
    for (const [label, value] of Object.entries(values)) {
      if (label === "Setting") await this.field(label).selectOption(value);
      else await this.field(label).fill(value);
    }
  }
  async addTag(name) {
    const input = this.dialog().getByRole("textbox", { name: "Add a tag", exact: true });
    await input.fill(name);
    await input.press("Enter");
  }
  async save() {
    await this.dialog()
      .getByRole("button", { name: "Save location", exact: true })
      .click();
  }
  async cancel() {
    await this.dialog()
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
  }
  async close() {
    await this.dialog()
      .getByRole("button", { name: "Close", exact: true })
      .click();
  }
  async escape() {
    await this.page.keyboard.press("Escape");
  }
  async clickBackdrop() {
    await this.page.mouse.click(2, 2);
  }
  async visitInspirationThenReturn() {
    await this.navigation()
      .getByRole("link", { name: "Inspiration", exact: true })
      .click();
    await expect(this.page).toHaveURL(/\/inspiration$/);
    await this.navigateFromLibrary();
    await expect(this.page).toHaveURL(/\/locations$/);
  }
  async navigateAway() {
    await this.page.evaluate(() => history.back());
  }
  async expectStayed() {
    await expect(this.page).toHaveURL(/\/locations$/);
    await expect(this.dialog()).toBeVisible();
  }
  async expectLeft() {
    await expect(this.page).toHaveURL(/\/inspiration$/);
  }
  async expectValue(label, value) {
    await expect(this.field(label)).toHaveValue(value);
  }
  async expectFieldError(label, message) {
    const field = this.field(label);
    await expect(field).toHaveAttribute("aria-invalid", "true");
    await expect(field).toBeFocused();
    await expect(this.dialog().getByText(message, { exact: true })).toBeVisible();
  }
  async expectNoFieldError(label) {
    await expect(this.field(label)).not.toHaveAttribute("aria-invalid", "true");
  }
  async expectSaveFailure() {
    await expect(this.dialog().getByRole("alert")).toContainText(
      "Couldn't save this location",
    );
  }
  async retrySave() {
    await this.dialog()
      .getByRole("button", { name: "Try again", exact: true })
      .click();
  }
  async expectDiscardChoice() {
    await expect(this.discardDialog()).toBeVisible();
    await expect(
      this.discardDialog().getByRole("button", { name: "Keep editing", exact: true }),
    ).toBeFocused();
  }
  async expectNoDiscardChoice() {
    await expect(this.discardDialog()).toHaveCount(0);
  }
  async keepEditing() {
    await this.discardDialog()
      .getByRole("button", { name: "Keep editing", exact: true })
      .click();
  }
  async discardChanges() {
    await this.discardDialog()
      .getByRole("button", { name: "Discard", exact: true })
      .click();
  }
  async expectClosed() {
    await expect(this.dialog()).toHaveCount(0);
  }
  async expectTriggerFocused(entry = "header") {
    await expect(this.addTrigger(entry)).toBeFocused();
  }
  async expectNotice(text) {
    await expect(this.page.getByRole("status")).toContainText(text);
  }
  async expectFocusContained() {
    for (let index = 0; index < 24; index++) {
      await this.page.keyboard.press("Tab");
      expect(
        await this.dialog().evaluate((dialog) => dialog.contains(document.activeElement)),
      ).toBe(true);
    }
  }
  async expectDialogFits() {
    const bounds = await this.dialog().boundingBox();
    const viewport = this.page.viewportSize();
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.y).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(viewport.width + 1);
    expect(bounds.y + bounds.height).toBeLessThanOrEqual(viewport.height + 1);
    if (viewport.width < 640) {
      expect(bounds.width).toBe(viewport.width);
      expect(Math.abs(bounds.y + bounds.height - viewport.height)).toBeLessThanOrEqual(1);
      expect(bounds.height).toBeLessThanOrEqual(viewport.height * 0.9 + 1);
    } else {
      expect(bounds.width).toBeLessThanOrEqual(560);
      expect(Math.abs(bounds.x + bounds.width / 2 - viewport.width / 2)).toBeLessThanOrEqual(1);
    }
    const save = this.dialog().getByRole("button", { name: "Save location", exact: true });
    await save.scrollIntoViewIfNeeded();
    await expect(save).toBeInViewport();
    await this.field("Name").scrollIntoViewIfNeeded();
    await expect(this.field("Name")).toBeInViewport();
  }
}
