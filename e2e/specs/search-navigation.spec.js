import { test, expect } from "@playwright/test";
import { SearchLibrary } from "../fixtures/search-library.js";
import { SearchPage } from "../page-objects/search-page.js";
import { MyWorkPage } from "../page-objects/my-work-page.js";
import { PhotographDetailPage } from "../page-objects/photograph-detail-page.js";

async function setup(page) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const library = new SearchLibrary(4);
  await library.attach(page, { details: true });
  return { screen: new SearchPage(page), work, library };
}

test("Given a noneditable area, when slash is pressed, then Search opens with field focus and repeating it retains the current query", async ({
  page,
}) => {
  const { screen, library } = await setup(page);
  await screen.open("?q=window");
  await screen.navigate("My Work");
  await screen.shortcut();
  await screen.expectInitial();
  await screen.expectSearchFocus();
  await screen.search("window");
  await screen.expectResults(4, 4);
  const requests = library.calls.length;
  await screen.shortcut();
  await screen.expectSearchFocus();
  await screen.expectQuery("window");
  await screen.typeSlashInQuery();
  await screen.expectQuery("window/");
  await screen.expectIgnoredShortcuts();
  await screen.filters();
  await screen.slashInDialog();
  await screen.cancelFilters();
  expect(library.calls).toHaveLength(requests);
});

test("Given unsaved notes, when slash navigation is canceled or confirmed, then route guards retain drafts or discard them before focusing Search", async ({
  page,
}) => {
  const { screen, work } = await setup(page);
  await screen.open();
  await screen.navigate("My Work");
  await work.openPhotograph("Study 01");
  const detail = new PhotographDetailPage(page);
  await detail.editNotes("Unfinished search notes");
  await screen.shortcut();
  await detail.expectDiscardChoice();
  await detail.keepEditing();
  await detail.expectNotes("Unfinished search notes");
  await screen.expectMainFocus();
  await screen.shortcut();
  await detail.discardChanges();
  await screen.expectInitial();
  await screen.expectSearchFocus();
  await screen.navigate("My Work");
  await work.openPhotograph("Study 01");
  await detail.expectNotes("Keep the edges quiet.\nTry a lower viewpoint.");
});

for (const width of [1440, 768, 640, 639, 320]) {
  test(`Given the shared shell at ${width}px, when each area is opened, then four destinations remain reachable with correct active state`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 });
    const { screen, work, library } = await setup(page);
    await screen.open("?q=window");
    await screen.expectResults(4, 4);
    await screen.expectNavigation("Search");
    for (const area of ["Photographers", "Inspiration", "My Work", "Search"]) {
      await screen.navigate(area);
      await screen.expectNavigation(area);
      await screen.expectMainFocus();
    }
    await screen.expectInitial();
    await screen.navigate("My Work");
    await work.openPhotograph("Study 01");
    await screen.expectNavigation("My Work");
    await screen.navigate("Search");
    await screen.search("window");
    await screen.openCard(library.items[0]);
    await screen.expectNavigation("Photographers");
    await screen.expectAccessible();
  });
}
