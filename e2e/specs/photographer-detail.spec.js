import { test, expect } from "@playwright/test";
import { MyWorkPage } from "../page-objects/my-work-page.js";
import { PhotographerLibrary } from "../fixtures/photographer-library.js";
import { PhotographerPage } from "../page-objects/photographer-page.js";
async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const library = new PhotographerLibrary(1);
  await library.attach(page);
  return { library, screen: new PhotographerPage(page) };
}
for (const width of [1440, 375])
  test(`photographer detail shows private bookmark and source at ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 });
    const { library, screen } = await setup(page);
    await screen.open();
    await screen.expectProfile(
      "Photographer 01",
      library.items[0].summary,
      "Study the window light.",
    );
    await screen.expectSource("https://portfolio1.example/work");
    await screen.expectEmpty();
    await screen.expectAccessible();
  });
test("photographer detail retries a failed load and pages linked references", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  library.failures = 1;
  library.linked = Array.from({ length: 25 }, (_, i) => ({
    id: "reference-" + i,
    title: "Study " + i,
    createdAt: "2026-08-12T12:00:00Z",
    previewUrl: null,
    width: null,
    height: null,
    sourceUrl: null,
    attribution: "Photographer 01",
  }));
  await screen.open();
  await screen.expectError();
  await screen.retry();
  await screen.expectReferences(24);
  await screen.more();
  await screen.expectReferences(25);
  expect(
    library.calls.filter((call) => call.operation === "references"),
  ).toHaveLength(2);
});

test("editing bookmark details updates the profile and preserves notes and tags", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.edit();
  await screen.changeDetails(
    "Casey Revised",
    "https://changed.example/",
    "An owner description",
  );
  await screen.saveDetails();
  await screen.expectDetailsClosed();
  await screen.expectProfile(
    "Casey Revised",
    "An owner description",
    "Study the window light.",
  );
  expect(library.items[0].tags).toHaveLength(2);
  await screen.expectSource("https://portfolio1.example/work", false);
});
test("canceling detail edits leaves the bookmark untouched", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.edit();
  await screen.changeDetails("Changed", "https://changed.example/", "Changed");
  await screen.cancelDetails();
  await screen.expectDetailsClosed();
  expect(library.calls.some((call) => call.operation === "update")).toBe(false);
});
test("failed and stale edits retain the draft and preserve newer unrelated notes", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.edit();
  await screen.changeDetails(
    "Casey Revised",
    "https://portfolio1.example/work",
    "My description",
  );
  library.failures = 1;
  await screen.saveDetails();
  await screen.expectSaveFailure();
  library.items[0].notes = "Newer notes";
  library.items[0].revision++;
  await screen.retrySave();
  await screen.reviewLatest();
  await screen.saveDetails();
  await screen.expectDetailsClosed();
  await screen.expectProfile("Casey Revised", "My description", "Newer notes");
});

test("delete confirmation defaults to Cancel and deletion retains linked references", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  library.linked = Array.from({ length: 6 }, (_, i) => ({
    id: "ref-" + i,
    title: "Study " + i,
    previewUrl: null,
  }));
  await screen.open();
  await screen.expectReferences(6);
  await screen.deleteBookmark();
  await screen.expectDeletion("Photographer 01", 6);
  await screen.cancelDeletion();
  expect(library.items).toHaveLength(1);
  await screen.deleteBookmark();
  await screen.confirmDeletion();
  await screen.expectDeleted();
  expect(library.items).toHaveLength(0);
  expect(library.retainedReferences).toHaveLength(6);
});
test("a stale delete requires reviewing the latest bookmark before confirmation", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.expectEmpty();
  await screen.deleteBookmark();
  library.items[0].name = "Changed elsewhere";
  library.items[0].revision++;
  await screen.confirmDeletion();
  await screen.reviewDeletion();
  await screen.expectDeletion("Changed elsewhere", 0);
  await screen.confirmDeletion();
  await screen.expectDeleted();
});

test("notes save independently without changing the profile description or tags", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.writeNotes("My new study notes");
  await screen.saveNotes();
  await screen.expectNotesSaved("My new study notes");
  expect(library.items[0].summary).toContain("Portraits in available light");
  expect(library.items[0].tags).toHaveLength(2);
});
test("failed notes retain the text and stale review preserves a newer name", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.writeNotes("Keep this draft");
  library.failures = 1;
  await screen.saveNotes();
  await screen.expectNotesFailure();
  library.items[0].name = "New name";
  library.items[0].revision++;
  await screen.saveNotes();
  await screen.reviewNotes();
  await screen.saveNotes();
  await screen.expectNotesSaved("Keep this draft");
  expect(library.items[0].name).toBe("New name");
});
