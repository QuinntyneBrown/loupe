import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { SignInPage } from "./sign-in-page.js";

export class SearchPage {
  constructor(page) {
    this.page = page;
  }
  navigation() {
    return this.page.getByRole("navigation", { name: "Library", exact: true });
  }
  async navigate(area) {
    await this.navigation()
      .getByRole("link", { name: area, exact: true })
      .click();
  }
  async expectNavigation(area) {
    await expect(this.navigation().getByRole("link")).toHaveCount(5);
    for (const name of [
      "My Work",
      "Inspiration",
      "Photographers",
      "Locations",
      "Search",
    ]) {
      const link = this.navigation().getByRole("link", { name, exact: true });
      await expect(link).toBeInViewport();
      if (area === name)
        await expect(link).toHaveAttribute("aria-current", "page");
      else await expect(link).not.toHaveAttribute("aria-current", "page");
    }
  }
  async expectMainFocus() {
    await expect(this.page.getByRole("main")).toBeFocused();
  }
  async shortcut() {
    await this.page.getByRole("main").focus();
    await this.page.keyboard.press("/");
  }
  async expectSearchFocus() {
    await expect(
      this.page.getByRole("searchbox", { name: "Search your library" }),
    ).toBeFocused();
  }
  async typeSlashInQuery() {
    await this.page
      .getByRole("searchbox", { name: "Search your library" })
      .press("/");
  }
  async expectIgnoredShortcuts() {
    await this.page.getByRole("main").focus();
    const url = this.page.url();
    for (const modifiers of [
      { ctrlKey: true },
      { altKey: true },
      { metaKey: true },
      { shiftKey: true },
      { isComposing: true },
      { repeat: true },
    ]) {
      const prevented = await this.page
        .getByRole("main")
        .evaluate((main, modifiers) => {
          const event = new KeyboardEvent("keydown", {
            key: "/",
            bubbles: true,
            cancelable: true,
            ...modifiers,
          });
          main.dispatchEvent(event);
          return event.defaultPrevented;
        }, modifiers);
      expect(prevented).toBe(false);
    }
    await expect(this.page).toHaveURL(url);
    await this.expectMainFocus();
  }
  async slashInDialog() {
    const focus = await this.dialog().evaluate((dialog) => {
      const before = document.activeElement;
      const event = new KeyboardEvent("keydown", {
        key: "/",
        bubbles: true,
        cancelable: true,
      });
      before.dispatchEvent(event);
      return {
        prevented: event.defaultPrevented,
        same: before === document.activeElement,
        inside: dialog.contains(document.activeElement),
      };
    });
    expect(focus).toEqual({ prevented: false, same: true, inside: true });
    await expect(this.dialog()).toBeVisible();
  }
  async open(query = "") {
    await this.page.goto("/search" + query);
    await new SignInPage(this.page).continue();
    await expect(this.page).toHaveURL(/\/search(?:\?|$)/);
  }
  async expectInitial() {
    await expect(
      this.page.getByRole("heading", {
        name: "Describe what you're looking for.",
      }),
    ).toBeVisible();
  }
  results() {
    return this.page.getByRole("region", { name: "Search results" });
  }
  card(title) {
    return this.results()
      .getByRole("article")
      .filter({
        has: this.page.getByRole("heading", { name: title, exact: true }),
      });
  }
  async openCard(item) {
    await this.card(item.title)
      .getByRole("link", {
        name: item.type === "reference" ? item.title : `Open ${item.title}`,
        exact: true,
      })
      .click({ position: { x: 8, y: 8 } });
    await expect(this.page).toHaveURL(
      new RegExp(
        `/${item.type === "reference" ? "inspiration" : "photographers"}/${item.id}$`,
      ),
    );
  }
  async expectCardSource(item, available = true) {
    const link = this.card(item.title).getByRole("link", {
      name:
        item.type === "reference"
          ? "Open source page"
          : /^Open (?!Window portrait)/,
    });
    if (!available) {
      await expect(link).toHaveCount(0);
      return;
    }
    await expect(link).toHaveAttribute("href", item.sourceUrl);
    await expect(link).toHaveAttribute("target", "_blank");
    await expect(link).toHaveAttribute("rel", "noopener noreferrer");
    await expect(link).toHaveAttribute("referrerpolicy", "no-referrer");
  }
  async openOriginalSource(item) {
    const before = this.page.url();
    const source = this.card(item.title).getByRole("link", {
      name:
        item.type === "reference"
          ? "Open source page"
          : `Open ${new URL(item.sourceUrl).hostname}`,
      exact: true,
    });
    const [popup] = await Promise.all([
      this.page.waitForEvent("popup"),
      source.click(),
    ]);
    await expect(popup).toHaveURL(item.sourceUrl);
    await expect(this.page).toHaveURL(before);
    await popup.close();
  }
  async expectPlaceholder(title, name = "No preview available") {
    await expect(
      this.card(title).getByRole("img", { name, exact: true }),
    ).toBeVisible();
  }
  async expectNoBoardActions() {
    await expect(
      this.results().getByRole("button", {
        name: /boards|Remove from this board/,
      }),
    ).toHaveCount(0);
  }
  async search(value) {
    const field = this.page.getByRole("searchbox", {
      name: "Search your library",
    });
    await field.fill(value);
    await field.press("Enter");
  }
  async type(value) {
    await this.page
      .getByRole("radiogroup", { name: "Type", exact: true })
      .getByRole("radio", { name: value, exact: true })
      .check();
  }
  async expectResults(count, total) {
    await expect(
      this.page
        .getByRole("region", { name: "Search results" })
        .getByRole("article"),
    ).toHaveCount(count);
    await expect(this.page.getByRole("status")).toContainText(
      `${total} results`,
    );
  }
  async more() {
    await this.page
      .getByRole("button", { name: "Load more", exact: true })
      .click();
  }
  async expectEmpty() {
    await expect(
      this.page.getByRole("heading", { name: /Nothing matched/ }),
    ).toBeVisible();
  }
  async editQuery() {
    await this.results()
      .getByRole("button", { name: "Edit query", exact: true })
      .click();
    await this.expectQueryFocused();
  }
  async expectQueryFocused() {
    await expect(
      this.page.getByRole("searchbox", { name: "Search your library" }),
    ).toBeFocused();
  }
  async expectNoRetry() {
    await expect(
      this.results().getByRole("button", { name: "Try again", exact: true }),
    ).toHaveCount(0);
  }
  async clearEmptyFilters() {
    await this.results()
      .getByRole("button", { name: "Clear filters", exact: true })
      .click();
  }
  async expectNoEmptyClear() {
    await expect(
      this.results().getByRole("button", {
        name: "Clear filters",
        exact: true,
      }),
    ).toHaveCount(0);
  }
  async expectCards(items) {
    await expect(
      this.results().getByRole("article").getByRole("heading", { level: 2 }),
    ).toHaveText(items.map((item) => item.title));
  }
  async expectEnd() {
    await expect(
      this.results().getByRole("button", { name: "Load more", exact: true }),
    ).toHaveCount(0);
  }
  async expectLoading() {
    await expect(this.results()).toHaveAttribute("aria-busy", "true");
  }
  async expectSettled() {
    await expect(this.results()).toHaveAttribute("aria-busy", "false");
  }
  async waitForResponses(count) {
    await expect
      .poll(() => this.page.evaluate(() => window.loupeSearchSettled ?? 0))
      .toBe(count);
    await this.page.evaluate(
      () =>
        new Promise((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(resolve)),
        ),
    );
  }
  async expectNoError() {
    await expect(this.page.getByRole("alert")).toHaveCount(0);
  }
  async expectInitialOnly() {
    await this.expectInitial();
    await this.expectNoResults();
  }
  async leaveForMyWork() {
    await this.page
      .getByRole("navigation", { name: "Library" })
      .getByRole("link", { name: "My Work", exact: true })
      .click();
    await expect(this.page).toHaveURL(/\/my-work$/);
  }
  async repeatAction(name) {
    if (name === "Submit") {
      await this.page
        .getByRole("searchbox", { name: "Search your library" })
        .press("Enter");
      await this.page
        .getByRole("searchbox", { name: "Search your library" })
        .press("Enter");
    } else {
      await this.results()
        .getByRole("button", { name, exact: true })
        .evaluate((button) => {
          button.click();
          button.click();
        });
    }
    await this.page.evaluate(
      () =>
        new Promise((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(resolve)),
        ),
    );
  }
  async expectCardFocused(item) {
    await expect(
      this.card(item.title).getByRole("link", {
        name: item.type === "reference" ? item.title : `Open ${item.title}`,
        exact: true,
      }),
    ).toBeFocused();
  }
  async expectEmptyFocused() {
    await expect(
      this.results().getByRole("heading", { name: /Nothing matched/ }),
    ).toBeFocused();
  }
  async draftQuery(value) {
    await this.page
      .getByRole("searchbox", { name: "Search your library" })
      .fill(value);
  }
  async expectStatus(message) {
    const status = this.results().getByRole("status");
    await expect(status).toHaveCount(1);
    await expect(status).toHaveText(message);
  }
  async expectCursorRefresh() {
    await expect(
      this.results().getByRole("heading", {
        name: "These results need refreshing.",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      this.results().getByRole("button", {
        name: "Refresh results",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      this.results().getByRole("button", {
        name: /^(Review filters|Try again)$/,
      }),
    ).toHaveCount(0);
  }
  async refreshResults() {
    await this.results()
      .getByRole("button", { name: "Refresh results", exact: true })
      .click();
  }
  async expectError() {
    await expect(
      this.page.getByRole("heading", {
        name: "Search isn't available right now.",
      }),
    ).toBeVisible();
  }
  async retry() {
    await this.page
      .getByRole("button", { name: "Try again", exact: true })
      .click();
  }
  async expectInvalid() {
    await expect(this.page.getByRole("alert")).toContainText("500 characters");
  }
  async expectSource() {
    const link = this.page
      .getByRole("link", { name: "Open source page" })
      .first();
    await expect(link).toHaveAttribute("target", "_blank");
    await expect(link).toHaveAttribute("rel", "noopener noreferrer");
    await expect(
      this.page.getByRole("button", { name: "Add to boards" }),
    ).toHaveCount(0);
  }
  async expectQuery(value) {
    await expect(
      this.page.getByRole("searchbox", { name: "Search your library" }),
    ).toHaveValue(value);
  }
  dialog() {
    return this.page.getByRole("dialog", {
      name: "Boards and tags",
      exact: true,
    });
  }
  async filters() {
    await this.page
      .getByRole("button", { name: "Boards and tags", exact: true })
      .click();
    await expect(this.dialog()).toBeVisible();
  }
  async toggleTag(name) {
    await this.dialog()
      .getByRole("group", { name: "Tags", exact: true })
      .getByRole("button", { name, exact: true })
      .click();
  }
  async toggleBoard(name) {
    await this.dialog()
      .getByRole("group", { name: "Boards", exact: true })
      .getByRole("button", { name, exact: true })
      .click();
  }
  async expectChoice(name, selected = false, group = "Tags") {
    await expect(
      this.dialog()
        .getByRole("group", { name: group, exact: true })
        .getByRole("button", { name, exact: true }),
    ).toHaveAttribute("aria-pressed", String(selected));
  }
  async expectNoChoice(name) {
    await expect(
      this.dialog()
        .getByRole("group", { name: "Tags", exact: true })
        .getByRole("button", { name, exact: true }),
    ).toHaveCount(0);
  }
  async expectTagChoices(names) {
    const choices = this.dialog().getByRole("group", {
      name: "Tags",
      exact: true,
    });
    await expect(choices.getByRole("button")).toHaveCount(names.length);
    for (const name of names)
      await expect(
        choices.getByRole("button", { name, exact: true }),
      ).toHaveCount(1);
  }
  async cancelFilters(trigger = "Boards and tags") {
    await this.dialog()
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
    await this.expectFiltersClosed(trigger);
  }
  async escapeFilters() {
    await this.dialog().press("Escape");
    await this.expectFiltersClosed();
  }
  async expectFiltersClosed(trigger = "Boards and tags") {
    await expect(this.dialog()).not.toBeVisible();
    await expect(
      this.page.getByRole("button", { name: trigger, exact: true }),
    ).toBeFocused();
  }
  async clearDraft() {
    await this.dialog()
      .getByRole("button", { name: "Clear filters", exact: true })
      .click();
  }
  async applyFilters() {
    await this.dialog()
      .getByRole("button", { name: "Apply filters", exact: true })
      .click();
    await expect(this.dialog()).not.toBeVisible();
  }
  async expectFilter(name) {
    await expect(
      this.page.getByRole("button", { name: `Remove ${name}`, exact: true }),
    ).toBeVisible();
  }
  async expectDialogError(message) {
    await expect(
      this.dialog().getByRole("alert").filter({ hasText: message }),
    ).toBeVisible();
  }
  async removeFilter(name) {
    await this.page
      .getByRole("button", { name: `Remove ${name}`, exact: true })
      .click();
  }
  async clearFilters() {
    await this.page
      .getByRole("button", { name: "Clear filters", exact: true })
      .first()
      .click();
  }
  async expectNoFilters() {
    await expect(
      this.page
        .getByRole("group", { name: "Selected filters" })
        .getByRole("button"),
    ).toHaveCount(0);
  }
  async expectType(name) {
    await expect(
      this.page
        .getByRole("radiogroup", { name: "Type", exact: true })
        .getByRole("radio", { name, exact: true }),
    ).toBeChecked();
  }
  async reload() {
    await this.page.reload();
    await new SignInPage(this.page).continue();
    await expect(this.page).toHaveURL(/\/search(?:\?|$)/);
  }
  async expectBoardExplanation() {
    await this.expectEmpty();
    await expect(
      this.page.getByText(
        "Photographers do not belong to boards. Remove the board filters or choose All or References.",
        { exact: true },
      ),
    ).toBeVisible();
  }
  async expectUnavailableBoard() {
    await expect(
      this.page.getByRole("heading", {
        name: "A selected board is unavailable.",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("button", { name: "Try again", exact: true }),
    ).toHaveCount(0);
  }
  async reviewFilters() {
    await this.page
      .getByRole("button", { name: "Review filters", exact: true })
      .click();
    await expect(this.dialog()).toBeVisible();
  }
  async retryTags() {
    await this.dialog()
      .getByRole("button", { name: "Retry tags", exact: true })
      .click();
  }
  async retryBoards() {
    await this.page
      .getByRole("button", { name: "Retry boards", exact: true })
      .click();
  }
  async expectPageError(message) {
    await expect(
      this.page.getByRole("alert").filter({ hasText: message }),
    ).toBeVisible();
  }
  async expectOptionsEmpty() {
    await expect(
      this.dialog().getByText("No active library tags yet.", { exact: true }),
    ).toBeVisible();
    await expect(
      this.dialog().getByText("No boards yet.", { exact: true }),
    ).toBeVisible();
  }
  async useKeyword() {
    await this.page
      .getByRole("button", { name: "Use Keyword", exact: true })
      .click();
  }
  async expectNoResults() {
    await expect(
      this.page.getByRole("region", { name: "Search results" }),
    ).toHaveCount(0);
  }
  async expectCorrection() {
    await expect(
      this.page.getByRole("heading", {
        name: "Check your search.",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("button", { name: "Try again", exact: true }),
    ).toHaveCount(0);
  }
  async restoreUrl(query) {
    await this.page.evaluate((value) => {
      history.pushState(null, "", "/search" + value);
      dispatchEvent(new PopStateEvent("popstate"));
    }, query);
  }
  async example(name) {
    await this.page
      .getByRole("button", { name: `“${name}”`, exact: true })
      .click();
  }
  async expectKeywordOnly() {
    await expect(
      this.page.getByText(
        "Keyword matches the words you type in your saved library.",
        { exact: true },
      ),
    ).toBeVisible();
    await expect(
      this.page.getByRole("button", { name: "Meaning", exact: true }),
    ).toHaveCount(0);
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
  async expectFilterHierarchy(board) {
    const boards = this.dialog().getByRole("heading", {
      name: "Boards",
      exact: true,
    });
    const tags = this.dialog().getByRole("heading", {
      name: "Tags",
      exact: true,
    });
    expect((await boards.boundingBox()).y).toBeLessThan(
      (await tags.boundingBox()).y,
    );
    await expect(
      this.dialog().getByRole("button", { name: board.name, exact: true }),
    ).toHaveAccessibleDescription(`${board.referenceCount} references`);
  }
  async expectDialogReachable() {
    for (const name of ["Close", "Cancel", "Apply filters"]) {
      const control = this.dialog().getByRole("button", { name, exact: true });
      await expect(control).toBeInViewport();
      const box = await control.boundingBox();
      expect(box.width).toBeGreaterThanOrEqual(24);
      expect(box.height).toBeGreaterThanOrEqual(24);
    }
    await this.dialog()
      .getByRole("button", { name: "Apply filters", exact: true })
      .focus();
    await this.page.keyboard.press("Tab");
    await expect(
      this.dialog().getByRole("button", { name: "Close", exact: true }),
    ).toBeFocused();
    await this.page.keyboard.press("Shift+Tab");
    await expect(
      this.dialog().getByRole("button", { name: "Apply filters", exact: true }),
    ).toBeFocused();
  }
  async expectGridColumns(columns) {
    const grid = this.results().locator(".results-grid").first();
    const layout = await grid.evaluate((element) => ({
      columns: getComputedStyle(element).gridTemplateColumns.split(" ").length,
      width: element.clientWidth,
      scroll: element.scrollWidth,
    }));
    expect(layout.columns).toBe(columns);
    expect(layout.scroll).toBeLessThanOrEqual(layout.width);
  }
  async increaseTextSpacing() {
    await this.page.addStyleTag({
      content:
        "* { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; } p { margin-bottom: 2em !important; }",
    });
  }
  async capture(path) {
    await this.page.screenshot({ path, fullPage: true });
  }
}
