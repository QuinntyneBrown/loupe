import { test } from "@playwright/test";
import { SearchLibrary } from "../fixtures/search-library.js";
import { SearchPage } from "../page-objects/search-page.js";
import { MyWorkPage } from "../page-objects/my-work-page.js";

for (const [width, columns] of [
  [320, 2],
  [375, 2],
  [639, 2],
  [640, 2],
  [768, 3],
  [1023, 4],
  [1024, 4],
  [1440, 5],
]) {
  test(`Given keyword search at ${width}px, when browsing and filtering, then mock layout and keyboard actions remain usable`, async ({
    page,
  }, info) => {
    await page.setViewportSize({ width, height: 900 });
    await page.emulateMedia({ reducedMotion: "reduce" });
    await new MyWorkPage(page).configureCollection(0);
    const library = new SearchLibrary(10);
    await library.attach(page);
    const screen = new SearchPage(page);
    await screen.open();
    await screen.expectInitial();
    await screen.expectAccessible();
    await screen.capture(info.outputPath(`search-initial-${width}.png`));
    await screen.search("window");
    await screen.expectResults(10, 10);
    await screen.expectGridColumns(columns);
    await screen.expectAccessible();
    await screen.capture(info.outputPath(`search-results-${width}.png`));
    await screen.filters();
    const board = library.boards[0];
    await screen.expectFilterHierarchy({
      ...board,
      referenceCount: library.items.filter((item) =>
        item.boardIds.includes(board.id),
      ).length,
    });
    await screen.expectDialogReachable();
    await screen.expectAccessible();
    await screen.capture(info.outputPath(`search-filters-${width}.png`));
    await screen.escapeFilters();
  });
}

for (const [width, height] of [
  [375, 667],
  [844, 390],
  [640, 800],
  [320, 800],
]) {
  test(`Given long saved metadata and increased text spacing at ${width}x${height}, when filtering then correcting errors, content reflows with reachable actions`, async ({
    page,
  }, info) => {
    await page.setViewportSize({ width, height });
    await page.emulateMedia({ reducedMotion: "reduce" });
    await new MyWorkPage(page).configureCollection(0);
    const library = new SearchLibrary(4);
    library.items[0].title = "Photographer ".repeat(10);
    library.items[0].sourceUrl = `https://${"p".repeat(55)}.example/saved`;
    library.items[1].title = "Long reference title ".repeat(8);
    library.items[1].previewUrl =
      "data:image/svg+xml," +
      encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" width="400" height="500"><rect width="400" height="500" fill="#ececf0"/><circle cx="200" cy="250" r="100" fill="#305c73"/></svg>',
      );
    library.items[1].width = 400;
    library.items[1].height = 500;
    library.items[1].tags.push("t".repeat(50));
    library.boards[0].name = "Long board name ".repeat(5).trim();
    await library.attach(page);
    const screen = new SearchPage(page);
    await screen.open("?q=window");
    await screen.expectResults(4, 4);
    await screen.increaseTextSpacing();
    await screen.expectAccessible();
    await screen.filters();
    await screen.expectDialogReachable();
    await screen.expectAccessible();
    await screen.capture(info.outputPath(`search-reflow-${width}.png`));
    await screen.toggleTag("t".repeat(50));
    await screen.applyFilters();
    await screen.expectResults(1, 1);
    await screen.search("absent");
    await screen.expectEmpty();
    await screen.expectAccessible();
    await screen.capture(info.outputPath(`search-empty-${width}.png`));
    await screen.editQuery();
    library.failures = 1;
    await screen.search("window");
    await screen.expectError();
    await screen.expectAccessible();
    await screen.capture(info.outputPath(`search-error-${width}.png`));
    await screen.retry();
    await screen.expectResults(1, 1);
  });
}
