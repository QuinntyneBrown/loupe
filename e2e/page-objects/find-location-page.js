import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { LocationSearchLibrary } from "../fixtures/location-search-library.js";
import { SignInPage } from "./sign-in-page.js";

export class FindLocationPage {
  constructor(page) {
    this.page = page;
  }
  async configure(count) {
    this.library = new LocationSearchLibrary(count);
    await this.library.attach(this.page);
  }
  async open(query = "") {
    await this.page.goto("/locations/find" + query);
    await new SignInPage(this.page).continue();
    await expect(this.page).toHaveURL(/\/locations\/find(?:\?|$)/);
  }
  async openFromLocations() {
    await this.page.goto("/locations");
    await new SignInPage(this.page).continue();
    await this.page
      .getByRole("link", { name: "Find a location", exact: true })
      .click();
    await expect(this.page).toHaveURL(/\/locations\/find$/);
  }
  async reload() {
    await this.page.reload();
    await new SignInPage(this.page).continue();
  }
  async back() {
    await this.page.goBack();
  }
  async forward() {
    await this.page.goForward();
  }
  main() {
    return this.page.getByRole("main");
  }
  navigation() {
    return this.page.getByRole("navigation", { name: "Library", exact: true });
  }
  async expectTitle() {
    await expect(this.page).toHaveTitle("Find a location · Loupe");
    await expect(
      this.navigation().getByRole("link", { name: "Locations", exact: true }),
    ).toHaveAttribute("aria-current", "page");
  }
  async expectInitial() {
    await expect(
      this.main().getByRole("heading", { level: 1, name: "Describe the shoot.", exact: true }),
    ).toBeVisible();
    await expect(this.main().getByText(/Keyword matches the words you type/)).toBeVisible();
    await expect(this.main().getByText(/locations without a report drop out/)).toBeVisible();
    await expect(this.main().getByRole("group", { name: "Example searches", exact: true }).getByRole("button")).toHaveCount(3);
    await expect(this.page.getByRole("alert")).toHaveCount(0);
    await expect(this.results()).toHaveCount(0);
    await expect(this.main().getByRole("link", { name: "Locations", exact: true })).toHaveAttribute("href", "/locations");
  }
  async expectMode(mode) {
    await expect(
      this.main()
        .getByRole("radiogroup", { name: "Search mode", exact: true })
        .getByRole("radio", { name: mode, exact: true }),
    ).toBeChecked();
  }
  async chooseMode(mode) {
    await this.main()
      .getByRole("radiogroup", { name: "Search mode", exact: true })
      .getByRole("radio", { name: mode, exact: true })
      .check();
  }
  searchbox() {
    return this.main().getByRole("searchbox", { name: "Describe the shoot", exact: true });
  }
  async search(value) {
    await this.searchbox().fill(value);
    await this.searchbox().press("Enter");
  }
  async expectQuery(value) {
    await expect(this.searchbox()).toHaveValue(value);
  }
  async expectQueryFocused() {
    await expect(this.searchbox()).toBeFocused();
  }
  async useExample(index) {
    await this.main()
      .getByRole("group", { name: "Example searches", exact: true })
      .getByRole("button")
      .nth(index)
      .click();
  }
  chip(group, name) {
    return this.main()
      .getByRole("group", { name: group, exact: true })
      .getByRole("button", { name, exact: true });
  }
  async expectChips(group, names) {
    await expect(this.main().getByRole("group", { name: group, exact: true }).getByRole("button")).toHaveText(names);
  }
  async toggleChip(group, name) {
    await this.chip(group, name).click();
  }
  async expectChip(group, name, pressed) {
    await expect(this.chip(group, name)).toHaveAttribute("aria-pressed", String(pressed));
  }
  people() {
    return this.main().getByRole("spinbutton", { name: "People", exact: true });
  }
  async setPeople(value) {
    await this.people().fill(value);
    await this.people().press("Enter");
  }
  async expectPeople(value) {
    await expect(this.people()).toHaveValue(value);
  }
  async chooseSetting(value) {
    await this.main()
      .getByRole("radiogroup", { name: "Setting", exact: true })
      .getByRole("radio", { name: value, exact: true })
      .check();
  }
  async expectSetting(value) {
    await expect(
      this.main()
        .getByRole("radiogroup", { name: "Setting", exact: true })
        .getByRole("radio", { name: value, exact: true }),
    ).toBeChecked();
  }
  filtersTrigger(name = "Tags") {
    return this.main().getByRole("button", { name, exact: true });
  }
  async expectFiltersTrigger(name, pressed) {
    await expect(this.filtersTrigger(name)).toBeVisible();
    await expect(this.filtersTrigger(name)).toHaveAttribute("aria-pressed", String(pressed));
  }
  async expectNoDesktopFilters() {
    await expect(this.main().getByRole("group", { name: "Shoot type", exact: true })).toBeHidden();
    await expect(this.main().getByRole("group", { name: "Time of day", exact: true })).toBeHidden();
    await expect(this.people()).toBeHidden();
    await expect(this.main().getByRole("radiogroup", { name: "Setting", exact: true })).toBeHidden();
  }
  async expectDesktopFilters() {
    await expect(this.main().getByRole("group", { name: "Shoot type", exact: true })).toBeVisible();
    await expect(this.main().getByRole("group", { name: "Time of day", exact: true })).toBeVisible();
    await expect(this.people()).toBeVisible();
    await expect(this.main().getByRole("radiogroup", { name: "Setting", exact: true })).toBeVisible();
  }
  async openFilters(name = "Tags") {
    await this.filtersTrigger(name).click();
    await expect(this.dialog()).toBeVisible();
  }
  dialog() {
    return this.page.getByRole("dialog", { name: "Filters", exact: true });
  }
  dialogChip(group, name) {
    const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    return this.dialog()
      .getByRole("group", { name: group, exact: true })
      .getByRole("button", { name: new RegExp(`^${escaped}( \\d+ locations?)?$`) });
  }
  async toggleDialogChip(group, name) {
    await this.dialogChip(group, name).click();
  }
  async expectDialogChip(group, name, pressed) {
    await expect(this.dialogChip(group, name)).toHaveAttribute("aria-pressed", String(pressed));
  }
  async expectTagChoices(entries) {
    const chips = this.dialog().getByRole("group", { name: "Tags", exact: true }).getByRole("button");
    await expect(chips).toHaveText(
      entries.map(([name, count]) => `${name} ${count} ${count === 1 ? "location" : "locations"}`),
    );
  }
  async expectTagsFailed() {
    await expect(this.dialog().getByRole("alert")).toContainText("Tags could not be loaded.");
    await this.dialog().getByRole("button", { name: "Retry tags", exact: true }).click();
  }
  async expectDialogError(message) {
    await expect(this.dialog().getByRole("alert").filter({ hasText: message })).toBeVisible();
  }
  async fillDialogPeople(value) {
    await this.dialog().getByRole("spinbutton", { name: /^People/ }).fill(value);
  }
  async chooseDialogSetting(value) {
    await this.dialog()
      .getByRole("radiogroup", { name: "Setting", exact: true })
      .getByRole("radio", { name: value, exact: true })
      .check();
  }
  async applyFilters() {
    await this.dialog().getByRole("button", { name: "Apply", exact: true }).click();
    await expect(this.dialog()).toHaveCount(0);
  }
  async clearDialog() {
    await this.dialog().getByRole("button", { name: "Clear", exact: true }).click();
  }
  async closeFilters() {
    await this.dialog().getByRole("button", { name: "Close", exact: true }).click();
    await expect(this.dialog()).toHaveCount(0);
  }
  async escapeFilters() {
    await this.page.keyboard.press("Escape");
    await expect(this.dialog()).toHaveCount(0);
  }
  async expectDialogFocusTrapped() {
    await expect(this.dialog().getByRole("button", { name: "Close", exact: true })).toBeFocused();
    await this.page.keyboard.press("Shift+Tab");
    await expect(this.dialog().getByRole("button", { name: "Apply", exact: true })).toBeFocused();
    await this.page.keyboard.press("Tab");
    await expect(this.dialog().getByRole("button", { name: "Close", exact: true })).toBeFocused();
  }
  async expectDialogReachable() {
    for (const name of ["Close", "Clear", "Apply"]) {
      const control = this.dialog().getByRole("button", { name, exact: true });
      await expect(control).toBeInViewport();
      const box = await control.boundingBox();
      expect(box.width).toBeGreaterThanOrEqual(24);
      expect(box.height).toBeGreaterThanOrEqual(24);
    }
  }
  async expectFocused(name) {
    await expect(this.main().getByRole("button", { name, exact: true })).toBeFocused();
  }
  async clearAll() {
    await this.main().getByRole("button", { name: "Clear all", exact: true }).click();
  }
  results() {
    return this.page.getByRole("region", { name: "Matching locations", exact: true });
  }
  async expectResultsHead(text) {
    await expect(this.results().getByRole("status").first()).toHaveText(text);
  }
  async expectMatchedByMeaning(shown) {
    await expect(this.results().getByText("Matched by meaning", { exact: true })).toHaveCount(shown ? 1 : 0);
  }
  async expectMeaningUnavailable() {
    await expect(
      this.results().getByRole("alert").getByRole("heading", { name: "Meaning search isn't available right now.", exact: true }),
    ).toBeVisible();
    await expect(this.results().getByText(/Keyword search still works with the same filters/)).toBeVisible();
  }
  async switchToKeyword() {
    await this.main().getByRole("button", { name: "Switch to Keyword", exact: true }).click();
  }
  async expectQueryRequired() {
    await expect(this.main().getByRole("alert")).toContainText("Describe the shoot to search by meaning.");
    await expect(this.searchbox()).toHaveAttribute("aria-invalid", "true");
    await expect(this.main().getByRole("heading", { name: "Describe the shoot to search by meaning.", exact: true })).toBeVisible();
    await expect(this.results()).toHaveCount(0);
  }
  async expectRefreshRequired() {
    const notice = this.results().getByRole("status").filter({ hasText: "Your locations changed while you were browsing." });
    await expect(notice).toBeVisible();
    await expect(notice.getByRole("button", { name: "Refresh results", exact: true })).toBeVisible();
  }
  async refreshResults() {
    await this.results().getByRole("button", { name: "Refresh results", exact: true }).click();
  }
  async expectNoRefreshRequired() {
    await expect(this.results().getByText("Your locations changed while you were browsing.")).toHaveCount(0);
  }
  async expectCardCount(count) {
    await expect(this.cards()).toHaveCount(count);
  }
  cards() {
    return this.results().getByRole("article");
  }
  card(name) {
    return this.cards().filter({ has: this.page.getByRole("heading", { name, exact: true }) });
  }
  async expectCards(names) {
    await expect(this.cards().getByRole("heading", { level: 2 })).toHaveText(names);
  }
  async expectCard(name, { cover = true, place, recommended = null, group = null, ratings = [], noReport = false }) {
    const card = this.card(name);
    await expect(card.getByRole("link", { name: `Open ${name}`, exact: true })).toHaveAttribute("href", /\/locations\//);
    if (cover) await expect(card.getByRole("img", { name, exact: true })).toBeVisible();
    else await expect(card.getByRole("img", { name: "No images yet", exact: true })).toBeVisible();
    await expect(card.getByText(place, { exact: true })).toBeVisible();
    if (recommended) await expect(card.getByText(`Recommended · ${recommended}`, { exact: true })).toBeVisible();
    else await expect(card.getByText(/^Recommended ·/)).toHaveCount(0);
    if (group) await expect(card.getByText(group, { exact: true })).toBeVisible();
    const pills = card.getByRole("list", { name: "Suitability", exact: true }).getByRole("listitem");
    await expect(pills).toHaveText(noReport ? ["No scouting report"] : ratings);
  }
  async openCard(name) {
    await this.card(name).getByRole("link", { name: `Open ${name}`, exact: true }).click({ position: { x: 8, y: 8 } });
    await expect(this.page).toHaveURL(/\/locations\/[^/?]+$/);
    await expect(this.page.getByRole("heading", { level: 1, name, exact: true })).toBeVisible();
  }
  async expectNoResults(query) {
    await expect(
      this.results().getByRole("heading", { name: query ? `No locations match “${query}”.` : "No locations match these filters.", exact: true }),
    ).toBeVisible();
    await expect(this.results().getByRole("button", { name: "Edit query", exact: true })).toBeVisible();
    await expect(this.page.getByRole("alert")).toHaveCount(0);
  }
  async editQuery() {
    await this.results().getByRole("button", { name: "Edit query", exact: true }).click();
  }
  async clearEmptyFilters() {
    await this.results().getByRole("button", { name: "Clear filters", exact: true }).click();
  }
  async expectNoEmptyClear() {
    await expect(this.results().getByRole("button", { name: "Clear filters", exact: true })).toHaveCount(0);
  }
  async expectError() {
    await expect(
      this.results().getByRole("alert").getByRole("heading", { name: "Find a location isn't available right now.", exact: true }),
    ).toBeVisible();
    await expect(this.results().getByRole("heading", { name: /No locations match/ })).toHaveCount(0);
  }
  async retry() {
    await this.results().getByRole("button", { name: "Try again", exact: true }).click();
  }
  async expectLoading() {
    await expect(this.results()).toHaveAttribute("aria-busy", "true");
  }
  async more() {
    await this.results().getByRole("button", { name: "Load more", exact: true }).click();
  }
  async expectEnd() {
    await expect(this.results().getByRole("button", { name: "Load more", exact: true })).toHaveCount(0);
  }
  async expectGridColumns(columns) {
    const grid = this.results().locator(".grid").first();
    const layout = await grid.evaluate((element) => ({
      columns: getComputedStyle(element).gridTemplateColumns.split(" ").length,
      width: element.clientWidth,
      scroll: element.scrollWidth,
    }));
    expect(layout.columns).toBe(columns);
    expect(layout.scroll).toBeLessThanOrEqual(layout.width);
  }
  lastSearch() {
    return this.library.calls.filter((call) => call.operation === "search").at(-1);
  }
  searchCount() {
    return this.library.calls.filter((call) => call.operation === "search").length;
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
      await this.page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
  }
  async capture(path) {
    await this.page.screenshot({ path, fullPage: true });
  }
}
