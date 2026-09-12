import { test, expect } from "@playwright/test";
import { SearchLibrary } from "../fixtures/search-library.js";
import { SearchPage } from "../page-objects/search-page.js";
import { MyWorkPage } from "../page-objects/my-work-page.js";
async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const library = new SearchLibrary();
  await library.attach(page);
  return { library, screen: new SearchPage(page) };
}

test("initial search explains the library and submits mixed keyword results with safe sources and paging", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.expectInitial();
  expect(library.calls).toHaveLength(0);
  await screen.search("window");
  await screen.expectResults(24, 26);
  await screen.expectSource();
  await screen.more();
  await screen.expectResults(26, 26);
  expect(library.calls.at(-1).cursor).toBe("24");
});
test("type and query are restored by Back and reload", async ({ page }) => {
  const { screen } = await setup(page);
  await screen.open("?q=window");
  await screen.expectResults(24, 26);
  await screen.type("Photographers");
  await screen.expectResults(6, 6);
  await screen.search("absent");
  await screen.expectEmpty();
  await page.goBack();
  await screen.expectQuery("window");
  await screen.expectResults(6, 6);
  await screen.open("?q=window&type=photographers");
  await screen.expectResults(6, 6);
});
test("invalid queries never reach the service and failure has a separate retry state", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.search("x".repeat(501));
  await screen.expectInvalid();
  expect(library.calls).toHaveLength(0);
  library.failures = 1;
  await screen.search("window");
  await screen.expectError();
  await screen.retry();
  await screen.expectResults(24, 26);
});
for (const width of [1440, 320])
  test(`search results are accessible at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const { screen } = await setup(page);
    await screen.open("?q=window");
    await screen.expectResults(24, 26);
    await screen.expectAccessible();
  });
test("initial search examples fit a narrow screen", async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 900 });
  const { screen } = await setup(page);
  await screen.open();
  await screen.expectInitial();
  await screen.expectAccessible();
});
