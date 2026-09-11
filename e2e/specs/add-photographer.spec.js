import { test, expect } from "@playwright/test";
import { MyWorkPage } from "../page-objects/my-work-page.js";
import { PhotographersPage } from "../page-objects/photographers-page.js";

async function setup(page, count = 0) {
  await new MyWorkPage(page).configureCollection(0);
  const screen = new PhotographersPage(page);
  await screen.configure(count);
  await screen.open();
  return screen;
}

test("read a portfolio, edit the preview and save its name, description, tags and notes", async ({
  page,
}) => {
  const screen = await setup(page);
  await screen.openAdd();
  await screen.readPortfolio("https://casey.example/");
  await screen.expectPreview();
  expect(screen.library.items).toHaveLength(0);
  await screen.editPreview(
    "Casey Revised",
    "My description",
    "Keep this study",
  );
  await screen.removeTag("film");
  await screen.addTag("window light");
  await screen.saveAdd();
  await screen.expectClosed();
  await screen.expectCount(1);
  expect(screen.library.items[0]).toMatchObject({
    name: "Casey Revised",
    summary: "My description",
    notes: "Keep this study",
  });
  expect(screen.library.items[0].tags.map((tag) => tag.name)).toEqual([
    "interiors",
    "window light",
  ]);
});
test("cancel while reading leaves no bookmark", async ({ page }) => {
  const screen = await setup(page);
  screen.library.drafts.hold = true;
  await screen.openAdd();
  await screen.readPortfolio("https://casey.example/");
  await screen.expectReading();
  await screen.cancelAdd();
  expect(screen.library.items).toHaveLength(0);
  expect(
    screen.library.drafts.calls.some((call) => call.operation === "cancel"),
  ).toBe(true);
});
test("a blocked page offers manual saving without inventing a site summary", async ({
  page,
}) => {
  const screen = await setup(page);
  screen.library.drafts.failure = "robots_disallowed";
  await screen.openAdd();
  await screen.readPortfolio("https://casey.example/");
  await screen.expectFallback();
  await screen.manualFallback("Casey", "Study these interiors");
  await screen.saveAdd();
  await screen.expectClosed();
  await screen.expectCount(1);
  expect(screen.library.items[0]).toMatchObject({
    name: "Casey",
    summary: null,
    notes: "Study these interiors",
  });
});
test("an existing portfolio is reported before another bookmark is created", async ({
  page,
}) => {
  const screen = await setup(page, 1);
  await screen.openAdd();
  await screen.readPortfolio("https://portfolio1.example/work#about");
  await screen.expectDuplicate();
  expect(screen.library.items).toHaveLength(1);
  await screen.cancelAdd();
});
test("a lost save response retains the draft and retries the same operation once", async ({
  page,
}) => {
  const screen = await setup(page);
  screen.library.drafts.lostSaveResponses = 1;
  await screen.openAdd();
  await screen.readPortfolio("https://casey.example/");
  await screen.expectPreview();
  await screen.editPreview("Casey", "My description", "My notes");
  await screen.saveAdd();
  await screen.expectSaveFailure();
  await screen.retryAdd();
  await screen.expectClosed();
  expect(screen.library.items).toHaveLength(1);
  const saves = screen.library.drafts.calls.filter(
    (call) => call.operation === "save",
  );
  expect(saves).toHaveLength(2);
  expect(saves[0].operationKey).toBe(saves[1].operationKey);
});

test("manual fallback retains the description supplied by its owner", async ({
  page,
}) => {
  const screen = await setup(page);
  screen.library.drafts.failure = "robots_disallowed";
  await screen.openAdd();
  await screen.readPortfolio("https://casey.example/");
  await screen.expectFallback();
  await screen.editPreview("Casey", "My own description", "My notes");
  await screen.saveAdd();
  await screen.expectClosed();
  expect(screen.library.items[0].summary).toBe("My own description");
});
for (const width of [1440, 375])
  test(`add dialog matches preview actions and is accessible at ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 812 });
    const screen = await setup(page);
    await screen.openAdd();
    await screen.expectAccessible();
    await screen.readPortfolio("https://casey.example/");
    await screen.expectPreview();
    await screen.expectPreviewActions();
    await screen.expectAccessible();
  });
test("invalid details offer correction without retrying a different operation", async ({
  page,
}) => {
  const screen = await setup(page);
  await screen.openAdd();
  await screen.readPortfolio("bad-url");
  await screen.expectValidationOnly();
});
