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

test("tag chips add and remove while preserving unsaved notes", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.writeNotes("Unsaved notes");
  await screen.addTag("interiors");
  await screen.expectTag("interiors");
  await screen.removeTag("portrait");
  await screen.expectNoTag("portrait");
  await screen.saveNotes();
  await screen.reviewNotes();
  await screen.saveNotes();
  await screen.expectNotesSaved("Unsaved notes");
  expect(library.items[0].tags.map((tag) => tag.name)).toEqual([
    "window light",
    "interiors",
  ]);
});
test("tag failures can retry and stale review retains another newly added tag", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.expectEmpty();
  library.failures = 1;
  await screen.addTag("interiors");
  await screen.expectTagError("Couldn't save");
  library.items[0].tags.push({
    name: "new elsewhere",
    category: null,
    provenance: "manual",
  });
  library.items[0].revision++;
  await screen.retryTag();
  await screen.reviewTags();
  await screen.retryTag();
  await screen.expectTag("interiors");
  await screen.expectTag("new elsewhere");
});
test("duplicate tag names are rejected without saving another variant", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.addTag(" WINDOW LIGHT ");
  await screen.expectTagError("already");
  expect(library.calls.some((call) => call.operation === "update")).toBe(false);
});

test("closing edit and deletion dialogs restores the More actions focus target", async ({
  page,
}) => {
  const { screen } = await setup(page);
  await screen.open();
  await screen.edit();
  await screen.cancelDetails();
  await screen.expectActionsFocused();
  await screen.deleteBookmark();
  await screen.cancelDeletion();
  await screen.expectActionsFocused();
});
test("unsaved notes can keep editing or explicitly discard before leaving", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.writeNotes("Private unfinished notes");
  await screen.backToCollection();
  await screen.keepEditing();
  await screen.saveNotes();
  await screen.expectNotesSaved("Private unfinished notes");
  await screen.writeNotes("Discard these");
  await screen.backToCollection();
  await screen.discardNotes();
  expect(library.items[0].notes).toBe("Private unfinished notes");
});

async function linkedSetup(page, count = 2) {
  const result = await setup(page);
  const { ReferenceLibrary } = await import("../fixtures/reference-library.js");
  const references = new ReferenceLibrary(count);
  references.items.forEach(
    (item) =>
      (item.photographer = {
        id: "photographer-1",
        name: "Photographer 01",
        portfolioUrl: "https://portfolio1.example/work",
      }),
  );
  await references.attach(page);
  result.library.referenceLibrary = references;
  return { ...result, references };
}
test("unlink preserves a reference and attribution and Undo restores the link", async ({
  page,
}) => {
  const { screen, references } = await linkedSetup(page);
  await screen.open();
  await screen.expectReferences(2);
  await screen.unlink("Reference 01");
  await screen.expectUnlinked();
  await screen.expectReferences(1);
  expect(references.items[0].photographer).toBeNull();
  expect(references.items[0].attribution).toBe("Supplied photographer");
  await screen.undoUnlink();
  await screen.expectReferences(2);
  expect(references.items[0].photographer.id).toBe("photographer-1");
});
test("Undo never overwrites a newer photographer assignment", async ({
  page,
}) => {
  const { screen, references } = await linkedSetup(page);
  await screen.open();
  await screen.expectReferences(2);
  await screen.unlink("Reference 01");
  await screen.expectUnlinked();
  references.items[0].photographer = {
    id: "another-photographer",
    name: "Another",
  };
  references.items[0].revision++;
  await screen.undoUnlink();
  await screen.expectUndoConflict();
  expect(references.items[0].photographer.id).toBe("another-photographer");
});

test("lost unlink response retries the completed change and still offers Undo", async ({
  page,
}) => {
  const { screen, references } = await linkedSetup(page);
  await screen.open();
  await screen.expectReferences(2);
  references.lostPhotographerResponses = 1;
  await screen.unlink("Reference 01");
  await screen.expectUnlinkFailure();
  await screen.retryUnlink();
  await screen.expectUnlinked();
  await screen.expectReferences(1);
  await screen.undoUnlink();
  await screen.expectReferences(2);
});
test("lost Undo response retries without leaving an already-restored reference hidden", async ({
  page,
}) => {
  const { screen, references } = await linkedSetup(page);
  await screen.open();
  await screen.expectReferences(2);
  await screen.unlink("Reference 01");
  await screen.expectUnlinked();
  references.lostPhotographerResponses = 1;
  await screen.undoUnlink();
  await screen.expectUndoFailure();
  await screen.undoUnlink();
  await screen.expectReferences(2);
});
test("unlink and Undo recover focus on the next and restored reference", async ({
  page,
}) => {
  const { screen } = await linkedSetup(page);
  await screen.open();
  await screen.expectReferences(2);
  await screen.unlink("Reference 01");
  await screen.expectReferenceFocused("Reference 02");
  await screen.undoUnlink();
  await screen.expectReferenceFocused("Reference 01");
});

test("link picker searches beyond page one and retains selections across queries", async ({
  page,
}) => {
  const { screen, references } = await linkedSetup(page, 26);
  references.items.forEach((item) => (item.photographer = null));
  await screen.open();
  await screen.expectEmpty();
  await screen.writeNotes("Keep this unfinished note");
  await screen.openLinkPicker();
  await screen.chooseReference("Reference 01");
  await screen.searchReferences("Reference 26");
  await screen.chooseReference("Reference 26");
  await screen.expectSelected(2);
  await screen.confirmLinks(2);
  await screen.expectReferences(2);
  await screen.saveNotes();
  await screen.expectNotesSaved("Keep this unfinished note");
  expect(
    references.items.filter(
      (item) => item.photographer?.id === "photographer-1",
    ),
  ).toHaveLength(2);
});
test("linking moves an existing assignment and cancel makes no link changes", async ({
  page,
}) => {
  const { screen, references } = await linkedSetup(page, 2);
  references.items.forEach(
    (item) =>
      (item.photographer = { id: "another-photographer", name: "Another" }),
  );
  await screen.open();
  await screen.expectEmpty();
  await screen.openLinkPicker();
  await screen.chooseReference("Reference 01");
  await screen.cancelLinkPicker();
  expect(references.items[0].photographer.id).toBe("another-photographer");
  await screen.openLinkPicker();
  await screen.chooseReference("Reference 01");
  await screen.confirmLinks(1);
  await screen.expectReferences(1);
  expect(references.items[0].photographer.id).toBe("photographer-1");
  expect(references.items[0].attribution).toBe("Supplied photographer");
});
