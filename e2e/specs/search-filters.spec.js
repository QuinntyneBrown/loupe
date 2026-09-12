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

for (const [left, leftKey, leftAlias, right, rightKey, rightAlias] of [
  ["straße", "STRAßE", " StrAße ", "strasse", "STRASSE", "StrAsSe"],
  ["ı", "ı", " ı ", "i", "I", " I "],
  ["ﬀ", "ﬀ", " ﬀ ", "ff", "FF", "Ff"],
  ["café", "CAFÉ", " Cafe\u0301 ", "portrait", "PORTRAIT", "PORTRAIT"],
])
  test(`Given distinct canonical tags ${left} and ${right}, URL aliases select independently and both filters require both tags`, async ({
    page,
  }) => {
    const { library, screen } = await setup(page);
    for (const [name, key, alias] of [
      [left, leftKey, leftAlias],
      [right, rightKey, rightAlias],
    ])
      for (const value of [name, alias, alias.normalize("NFC").trim()])
        library.tagIdentities.set(value, key);
    library.items = library.items.slice(0, 7);
    library.items.forEach((item, index) => {
      item.tags =
        index === 0
          ? [left]
          : index === 1
            ? [right]
            : index === 5 || index === 6
              ? [left, right]
              : [];
    });
    await screen.open();
    await screen.filters();
    await screen.expectChoice(left);
    await screen.expectChoice(right);
    await screen.cancelFilters();

    await screen.open(
      `?tags=${encodeURIComponent(leftAlias)}&tags=${encodeURIComponent(left)}`,
    );
    await screen.expectResults(3, 3);
    await screen.filters();
    await screen.expectChoice(left, true);
    await screen.expectChoice(right, false);
    await screen.expectTagChoices([left, right]);
    await screen.toggleTag(right);
    await screen.expectChoice(left, true);
    await screen.expectChoice(right, true);
    await screen.applyFilters();
    await screen.expectResults(2, 2);
    await screen.expectCards([library.items[5], library.items[6]]);
    expect(new URL(page.url()).searchParams.getAll("tags")).toEqual([
      leftAlias,
      left,
      right,
    ]);
    await screen.filters();
    await screen.toggleTag(left);
    await screen.expectChoice(left, false);
    await screen.expectChoice(right, true);
    await screen.applyFilters();
    await screen.expectResults(3, 3);
    expect(new URL(page.url()).searchParams.getAll("tags")).toEqual([right]);

    await screen.open(
      `?tags=${encodeURIComponent(leftAlias)}&tags=${encodeURIComponent(rightAlias)}`,
    );
    await screen.expectResults(2, 2);
    await screen.filters();
    await screen.expectChoice(left, true);
    await screen.expectChoice(right, true);
    await screen.toggleTag(right);
    await screen.applyFilters();
    await screen.expectResults(3, 3);
    expect(new URL(page.url()).searchParams.getAll("tags")).toEqual([
      leftAlias,
    ]);
    await screen.removeFilter(leftAlias);
    await screen.expectResults(7, 7);
  });

test("Given an initial keyword page, examples submit their displayed text and type selection starts browsing", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.expectInitial();
  await screen.expectKeywordOnly();
  expect(library.calls).toHaveLength(0);
  expect(library.tagCalls).toBe(0);
  expect(library.boardCalls).toBe(0);
  const example = "Moody portraits with soft window light";
  await screen.example(example);
  await screen.expectEmpty();
  expect(library.calls.at(-1).query).toBe(example);
  await screen.open();
  await screen.type("Photographers");
  await screen.expectResults(6, 6);
});

test("Given mixed-library choices, dialog edits remain draft until Apply and Cancel or Escape restore focus", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  const initial = page.url();
  await screen.filters();
  await screen.expectChoice("cinematic");
  await screen.toggleTag("portrait");
  await screen.toggleBoard("Portrait study");
  expect(page.url()).toBe(initial);
  expect(library.calls).toHaveLength(0);
  await screen.cancelFilters();
  await screen.filters();
  await screen.expectChoice("portrait", false);
  await screen.expectChoice("Portrait study", false, "Boards");
  await screen.toggleTag("cinematic");
  await screen.escapeFilters();
  await screen.filters();
  await screen.expectChoice("cinematic", false);
  await screen.toggleTag("portrait");
  await screen.toggleBoard("Portrait study");
  await screen.applyFilters();
  await screen.expectResults(7, 7);
  await screen.expectFilter("portrait");
  await screen.expectFilter("Portrait study");
  const applied = page.url(),
    calls = library.calls.length;
  await screen.filters();
  await screen.clearDraft();
  await screen.expectChoice("portrait", false);
  await screen.expectChoice("Portrait study", false, "Boards");
  expect(page.url()).toBe(applied);
  expect(library.calls).toHaveLength(calls);
  await screen.cancelFilters();
  await screen.expectFilter("portrait");
});

test("Given query and type, applying multiple tags AND multiple boards OR carries every group", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open("?q=window&type=references");
  await screen.expectResults(20, 20);
  await screen.filters();
  await screen.toggleTag("portrait");
  await screen.toggleTag("soft light");
  await screen.toggleBoard("Portrait study");
  await screen.toggleBoard("Window light");
  await screen.applyFilters();
  await screen.expectResults(7, 7);
  expect(library.calls.at(-1)).toMatchObject({
    query: "window",
    type: "references",
    tags: ["portrait", "soft light"],
    boardIds: library.boards.slice(0, 2).map((board) => board.id),
  });
  const url = new URL(page.url());
  expect(url.searchParams.getAll("tags")).toEqual(["portrait", "soft light"]);
  expect(url.searchParams.getAll("boardIds")).toEqual(
    library.boards.slice(0, 2).map((board) => board.id),
  );
});

test("Given ten selected tags or boards, an eleventh gets a field error without dropping selections", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open();
  await screen.filters();
  for (let index = 1; index <= 10; index++)
    await screen.toggleTag(`study ${index}`);
  await screen.toggleTag("study 11");
  await screen.expectDialogError("Choose up to 10 tags.");
  await screen.expectChoice("study 11", false);
  for (let index = 1; index <= 10; index++)
    await screen.expectChoice(`study ${index}`, true);
  for (const board of library.boards.slice(0, 10))
    await screen.toggleBoard(board.name);
  await screen.toggleBoard("Board 11");
  await screen.expectDialogError("Choose up to 10 boards.");
  await screen.expectChoice("Board 11", false, "Boards");
  for (const board of library.boards.slice(0, 10))
    await screen.expectChoice(board.name, true, "Boards");
  await screen.applyFilters();
  await screen.expectEmpty();
  expect(library.calls.at(-1).tags).toHaveLength(10);
  expect(library.calls.at(-1).boardIds).toHaveLength(10);
});

test("Given eleven URL tag aliases, bounded facet reads reconcile every selection and allow independent correction", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  const aliases = Array.from(
    { length: 11 },
    (_, index) => `Study ${index + 1}`,
  );
  for (const [index, alias] of aliases.entries())
    library.tagIdentities.set(alias, `STUDY ${index + 1}`);
  await screen.open(
    `?q=window&${aliases.map((tag) => `tags=${encodeURIComponent(tag)}`).join("&")}`,
  );
  await screen.expectPageError("Choose up to 10 tags.");
  const original = page.url();
  await screen.filters();
  await screen.expectChoice("cinematic");
  for (let index = 1; index <= 11; index++)
    await screen.expectChoice(`study ${index}`, true);
  expect(page.url()).toBe(original);
  expect(library.calls).toHaveLength(0);
  expect(library.tagSelections.flat()).toEqual(aliases);
  expect(library.tagSelections.every((batch) => batch.length <= 10)).toBe(true);
  await screen.toggleTag("study 11");
  await screen.expectChoice("study 11", false);
  for (let index = 1; index <= 10; index++)
    await screen.expectChoice(`study ${index}`, true);
  await screen.applyFilters();
  await screen.expectEmpty();
  expect(new URL(page.url()).searchParams.getAll("tags")).toEqual(
    aliases.slice(0, 10),
  );
  expect(library.calls.at(-1).tags).toEqual(aliases.slice(0, 10));
});

for (const value of ["", " \t ", "x".repeat(51), "bad\0tag"])
  test(`Given invalid URL tag ${JSON.stringify(value)}, real facets load and the invalid selection remains explicitly removable in the dialog`, async ({
    page,
  }) => {
    const { library, screen } = await setup(page);
    await screen.open();
    await screen.restoreUrl(
      `?q=window&tags=${encodeURIComponent(value)}&tags=%20PORTRAIT%20`,
    );
    await screen.expectPageError(
      "Use tag names of 1–50 characters without null characters.",
    );
    const original = page.url();
    await screen.filters();
    await screen.expectChoice("cinematic");
    await screen.expectChoice("portrait", true);
    await screen.expectChoice(`Invalid tag: ${JSON.stringify(value)}`, true);
    expect(page.url()).toBe(original);
    expect(library.calls).toHaveLength(0);
    expect(library.tagSelections.flat()).toEqual([" PORTRAIT "]);
    await screen.toggleTag(`Invalid tag: ${JSON.stringify(value)}`);
    await screen.applyFilters();
    await screen.expectResults(24, 26);
    expect(new URL(page.url()).searchParams.getAll("tags")).toEqual([
      " PORTRAIT ",
    ]);
    await screen.restoreUrl(`?q=window&tags=${encodeURIComponent(value)}`);
    await screen.filters();
    await screen.expectChoice("cinematic");
    await screen.expectChoice(`Invalid tag: ${JSON.stringify(value)}`, true);
    await screen.clearDraft();
    await screen.applyFilters();
    await screen.expectResults(24, 26);
    await screen.expectNoFilters();
  });

test("Given active URL filters, removing and clearing preserve query and type through Back, Forward and reload", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open(
    `?q=window&type=references&tags=portrait&tags=soft%20light&boardIds=${library.boards[0].id}&boardIds=${library.boards[1].id}`,
  );
  await screen.expectResults(7, 7);
  await screen.expectFilter("Portrait study");
  await screen.expectFilter("Window light");
  const original = page.url();
  await screen.removeFilter("soft light");
  await screen.expectResults(14, 14);
  expect(new URL(page.url()).searchParams.getAll("tags")).toEqual(["portrait"]);
  await screen.removeFilter("Window light");
  await screen.expectResults(7, 7);
  await screen.clearFilters();
  await screen.expectResults(20, 20);
  await screen.expectNoFilters();
  await screen.expectQuery("window");
  await screen.expectType("References");
  const cleared = page.url();
  await page.goBack();
  await screen.expectFilter("Portrait study");
  await screen.expectResults(7, 7);
  await page.goForward();
  await screen.expectResults(20, 20);
  expect(page.url()).toBe(cleared);
  await page.goBack();
  await page.goBack();
  await page.goBack();
  await screen.expectResults(7, 7);
  expect(page.url()).toBe(original);
  await screen.reload();
  await screen.expectFilter("soft light");
  await screen.expectFilter("Window light");
  await screen.expectResults(7, 7);
  expect(page.url()).toBe(original);
});

test("Given a filters-only URL, browsing starts and clearing filters resets paged results without erasing type", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open("?tags=portrait");
  await screen.expectResults(24, 26);
  await screen.more();
  await screen.expectResults(26, 26);
  await screen.removeFilter("portrait");
  await screen.expectResults(24, 26);
  expect(library.calls.at(-1).cursor).toBeUndefined();
  await screen.expectQuery("");
  await screen.filters();
  await screen.toggleTag("cinematic");
  await screen.applyFilters();
  await screen.expectResults(6, 6);
});

test("Given Photographers and a board, the empty result explains the combination and retains both filters", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open(
    `?q=window&type=photographers&boardIds=${library.boards[0].id}`,
  );
  await screen.expectBoardExplanation();
  await screen.expectFilter("Portrait study");
  await screen.expectType("Photographers");
  expect(library.calls.at(-1).boardIds).toEqual([library.boards[0].id]);
  await screen.removeFilter("Portrait study");
  await screen.expectResults(6, 6);
  await screen.expectQuery("window");
  await screen.expectType("Photographers");
});

test("Given unknown tags and unavailable boards, filters remain represented until explicitly removed", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open("?q=window&tags=unknown");
  await screen.expectEmpty();
  await screen.expectFilter("unknown");
  await screen.filters();
  await screen.expectChoice("unknown", true);
  await screen.cancelFilters();
  await screen.removeFilter("unknown");
  await screen.expectResults(24, 26);
  const unavailable = "30000000-0000-4000-8000-999999999999";
  await screen.open(
    `?q=window&type=references&tags=portrait&boardIds=${unavailable}`,
  );
  await screen.expectUnavailableBoard();
  await screen.expectFilter("Unavailable board");
  expect(new URL(page.url()).searchParams.getAll("boardIds")).toEqual([
    unavailable,
  ]);
  expect(library.calls.at(-1).boardIds).toEqual([unavailable]);
  await screen.reviewFilters();
  await screen.expectChoice("Unavailable board", true, "Boards");
  await screen.cancelFilters("Review filters");
  await screen.removeFilter("Unavailable board");
  await screen.expectResults(20, 20);
  await screen.expectFilter("portrait");
  await screen.expectType("References");
});

test("Given failed option reads, retry tags and boards independently without treating failure as an empty library", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  library.tagFailures = 1;
  library.boardFailures = 1;
  await screen.open("?q=window&tags=portrait");
  await screen.expectResults(24, 26);
  const calls = library.calls.length;
  await screen.filters();
  await screen.expectDialogError("Tags could not be loaded.");
  await screen.expectDialogError("Boards could not be loaded.");
  await screen.expectChoice("portrait", true);
  await screen.retryTags();
  await screen.expectChoice("cinematic");
  await screen.expectDialogError("Boards could not be loaded.");
  await screen.retryBoards();
  await screen.expectChoice("Portrait study", false, "Boards");
  await screen.cancelFilters();
  expect(library.tagCalls).toBe(2);
  expect(library.boardCalls).toBe(2);
  expect(library.calls).toHaveLength(calls);
  await screen.expectFilter("portrait");
  library.items = [];
  library.boards = [];
  await screen.filters();
  await screen.expectOptionsEmpty();
  await screen.expectChoice("portrait", true);
  await screen.cancelFilters();
});

test("Given failed board label resolution, retain the ID and offer retry rather than declaring the board deleted", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  library.boardFailures = 1;
  await screen.open(`?q=window&boardIds=${library.boards[0].id}`);
  await screen.expectResults(7, 7);
  await screen.expectPageError("Board names could not be loaded.");
  await screen.expectFilter("Board name unavailable");
  const calls = library.calls.length;
  await screen.retryBoards();
  await screen.expectFilter("Portrait study");
  expect(library.calls).toHaveLength(calls);
  expect(new URL(page.url()).searchParams.getAll("boardIds")).toEqual([
    library.boards[0].id,
  ]);
});

test("Given an invalid type URL, correct the type explicitly while preserving query and filters", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open("?q=window&type=other&tags=portrait");
  await screen.expectPageError("Choose All, References or Photographers.");
  await screen.expectNoResults();
  expect(library.calls).toHaveLength(0);
  await screen.search("window");
  await screen.expectPageError("Choose All, References or Photographers.");
  expect(library.calls).toHaveLength(0);
  await screen.type("References");
  await screen.expectResults(20, 20);
  await screen.expectFilter("portrait");
});

for (const value of ["", "x".repeat(51), "bad\0tag"])
  test(`Given invalid tag ${JSON.stringify(value)}, retain it for explicit correction without a search request`, async ({
    page,
  }) => {
    const { library, screen } = await setup(page);
    await screen.open();
    await screen.restoreUrl(
      `?q=window&type=references&tags=${encodeURIComponent(value)}`,
    );
    await screen.expectPageError(
      "Use tag names of 1–50 characters without null characters.",
    );
    await screen.expectNoResults();
    expect(library.calls).toHaveLength(0);
    await screen.clearFilters();
    await screen.expectResults(20, 20);
    await screen.expectQuery("window");
    await screen.expectType("References");
  });

test("Given too many URL tags or boards, retain every value and allow correction without silently truncating", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  const tags = Array.from(
    { length: 11 },
    (_, index) => `tags=study%20${index + 1}`,
  ).join("&");
  await screen.open(`?q=window&${tags}`);
  await screen.expectPageError("Choose up to 10 tags.");
  expect(library.calls).toHaveLength(0);
  await screen.expectFilter("study 11");
  await screen.removeFilter("study 11");
  await screen.expectEmpty();
  expect(library.calls.at(-1).tags).toHaveLength(10);
  const calls = library.calls.length;
  await screen.open(
    `?q=window&${library.boards.map((board) => `boardIds=${board.id}`).join("&")}`,
  );
  await screen.expectPageError("Choose up to 10 boards.");
  expect(library.calls).toHaveLength(calls);
  await screen.expectFilter("Board 11");
  await screen.removeFilter("Board 11");
  await screen.expectResults(20, 20);
  expect(library.calls.at(-1).boardIds).toHaveLength(10);
});

test("Given an invalid board ID, offer explicit removal without silently broadening the URL", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open(
    "?q=window&type=references&boardIds=not-a-board&tags=portrait",
  );
  await screen.expectPageError("Choose valid board IDs.");
  await screen.expectNoResults();
  expect(library.calls).toHaveLength(0);
  expect(new URL(page.url()).searchParams.getAll("boardIds")).toEqual([
    "not-a-board",
  ]);
  await screen.removeFilter("Invalid board ID");
  await screen.expectResults(20, 20);
  await screen.expectFilter("portrait");
});

test("Given explicit keyword mode, preserve it through submissions, filter changes, history and reload", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open("?q=window&mode=keyword&tags=portrait");
  await screen.expectResults(24, 26);
  expect(library.calls.at(-1).mode).toBe("keyword");
  await screen.type("References");
  await screen.expectResults(20, 20);
  await screen.clearFilters();
  await screen.expectResults(20, 20);
  expect(new URL(page.url()).searchParams.get("mode")).toBe("keyword");
  await screen.search("soft");
  await screen.expectResults(20, 20);
  expect(library.calls.at(-1).mode).toBe("keyword");
  await page.goBack();
  await screen.expectQuery("window");
  await screen.reload();
  await screen.expectResults(20, 20);
  expect(new URL(page.url()).searchParams.get("mode")).toBe("keyword");
});

for (const mode of ["meaning", "other", ""])
  test(`Given unsupported mode ${JSON.stringify(mode)}, only explicit Keyword correction starts search and preserves other state`, async ({
    page,
  }) => {
    const { library, screen } = await setup(page);
    await screen.open(
      `?q=window&type=references&tags=portrait&boardIds=${library.boards[0].id}&mode=${mode}`,
    );
    await screen.expectPageError("Only keyword search is supported.");
    await screen.expectNoResults();
    expect(library.calls).toHaveLength(0);
    await screen.search("window");
    await screen.expectPageError("Only keyword search is supported.");
    expect(library.calls).toHaveLength(0);
    await screen.useKeyword();
    await screen.expectResults(7, 7);
    await screen.expectType("References");
    await screen.expectFilter("portrait");
    await screen.expectFilter("Portrait study");
    expect(library.calls.at(-1)).toMatchObject({
      query: "window",
      type: "references",
      mode: "keyword",
      tags: ["portrait"],
      boardIds: [library.boards[0].id],
    });
  });

test("Given API validation fields, distinguish corrective errors from retryable search failure and retain the filter", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  library.errors.push({
    error: "invalid_request",
    errors: { tags: ["Review the selected tags."] },
  });
  await screen.open("?q=window&type=references&tags=portrait");
  await screen.expectCorrection();
  await screen.expectPageError("Review the selected tags.");
  await screen.expectFilter("portrait");
  expect(library.calls).toHaveLength(1);
  await screen.reviewFilters();
  await screen.clearDraft();
  await screen.applyFilters();
  await screen.expectResults(20, 20);
  library.errors.push({
    error: "invalid_request",
    errors: { query: ["Review the query text."] },
  });
  await screen.search("portrait");
  await screen.expectCorrection();
  await screen.expectPageError("Review the query text.");
  await screen.search("window");
  await screen.expectResults(20, 20);
});

for (const width of [1440, 320])
  test(`Given a filter dialog at ${width}px, all library choices and selected long tags remain accessible`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 700 });
    const { screen } = await setup(page);
    await screen.open(`?tags=${"x".repeat(50)}`);
    await screen.expectEmpty();
    await screen.filters();
    await screen.expectChoice("x".repeat(50), true);
    await screen.expectChoice("study 11");
    await screen.expectAccessible();
    await screen.escapeFilters();
  });

test("Given repeated singular URL fields, reject ambiguity until each field is explicitly corrected", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open(
    "?q=window&q=portrait&type=references&type=photographers&mode=keyword&mode=meaning&tags=portrait",
  );
  await screen.expectPageError("Use one query value.");
  await screen.expectPageError("Choose one type.");
  await screen.expectPageError("Choose one search mode.");
  await screen.expectNoResults();
  expect(library.calls).toHaveLength(0);
  await screen.useKeyword();
  await screen.expectPageError("Use one query value.");
  await screen.expectPageError("Choose one type.");
  expect(library.calls).toHaveLength(0);
  expect(new URL(page.url()).searchParams.getAll("q")).toEqual([
    "window",
    "portrait",
  ]);
  expect(new URL(page.url()).searchParams.getAll("type")).toEqual([
    "references",
    "photographers",
  ]);
  await screen.type("All");
  await screen.expectPageError("Use one query value.");
  expect(library.calls).toHaveLength(0);
  await screen.search("window");
  await screen.expectResults(24, 26);
  await screen.expectFilter("portrait");
  expect(library.calls.at(-1).mode).toBe("keyword");
});

test("Given equivalent tag spelling and board ID casing, resolve labels and resubmit the same normalized search", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  const original = library.boards[0].id,
    id = "aaaaaaaa-0000-4000-8000-000000000001";
  library.boards[0].id = id;
  for (const item of library.items)
    item.boardIds = item.boardIds.map((value) =>
      value === original ? id : value,
    );
  await screen.open(
    `?q=window&tags=%20PORTRAIT%20&boardIds=${id.toUpperCase()}`,
  );
  await screen.expectResults(7, 7);
  await screen.expectFilter("Portrait study");
  await screen.search("window");
  await expect.poll(() => library.calls.length).toBe(2);
  expect(library.calls.at(-1)).toMatchObject({
    tags: ["PORTRAIT"],
    boardIds: [id],
  });
});

test("Given active tags beyond the loaded result page and pending suggestions, every active library tag remains selectable", async ({
  page,
}) => {
  await new MyWorkPage(page).configureCollection(0);
  const library = new SearchLibrary(80);
  library.items.at(-1).tags.push("distant active");
  library.items[0].pendingTags = ["pending only"];
  await library.attach(page);
  const screen = new SearchPage(page);
  await screen.open("?q=window");
  await screen.expectResults(24, 80);
  await screen.filters();
  await screen.expectChoice("distant active");
  await screen.expectNoChoice("pending only");
  await screen.toggleTag("distant active");
  await screen.applyFilters();
  await screen.expectResults(1, 1);
});
