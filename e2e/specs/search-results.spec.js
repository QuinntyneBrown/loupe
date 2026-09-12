import { test, expect } from "@playwright/test";
import { SearchLibrary } from "../fixtures/search-library.js";
import { SearchPage } from "../page-objects/search-page.js";
import { MyWorkPage } from "../page-objects/my-work-page.js";
import { ReferenceDetailPage } from "../page-objects/reference-detail-page.js";
import { PhotographerPage } from "../page-objects/photographer-page.js";

async function setup(page, library = new SearchLibrary(), options) {
  await new MyWorkPage(page).configureCollection(0);
  await library.attach(page, options);
  return { library, screen: new SearchPage(page) };
}

test("Given mixed saved results, when cards open, then their real details use shared IDs and sources stay separate without board writes", async ({
  page,
}) => {
  const library = new SearchLibrary(4);
  library.items[1].id = library.items[0].id;
  const { screen } = await setup(page, library, { details: true });
  await page.context().route("https://portfolio.example/**", (route) =>
    route.fulfill({
      contentType: "text/html",
      body: "<title>Original source</title>",
    }),
  );
  const httpCalls = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.startsWith("/api/"))
      httpCalls.push(request.url());
  });
  await screen.open("?q=window");
  await screen.expectResults(4, 4);
  await screen.expectNoBoardActions();
  for (const item of library.items.slice(0, 2)) {
    await screen.expectCardSource(item);
    await screen.openOriginalSource(item);
    await screen.expectCards(library.items);
    await screen.openCard(item);
    if (item.type === "reference") {
      await new ReferenceDetailPage(page).expectSaved(
        library.references.items.find((saved) => saved.id === item.id),
      );
    } else {
      const photographer = library.photographers.items.find(
        (saved) => saved.id === item.id,
      );
      await new PhotographerPage(page).expectProfile(
        photographer.name,
        photographer.summary,
        photographer.notes,
      );
    }
    await page.goBack();
    await screen.expectResults(4, 4);
  }
  expect(library.references.calls).toContain("get");
  expect(library.photographers.calls).toContainEqual(
    expect.objectContaining({
      operation: "get",
      id: library.items[0].id,
    }),
  );
  expect(
    library.references.boardCalls.every((call) => call.operation === "list"),
  ).toBe(true);
  expect(httpCalls).toEqual([]);
});

test("Given missing or broken reference previews and unsafe sources, when results render, then useful placeholders replace media and unsafe links are absent", async ({
  page,
}) => {
  const library = new SearchLibrary(3);
  library.items[0].sourceUrl = "javascript:alert(1)";
  library.items[1].sourceUrl = "data:text/html,unsafe";
  library.items[2].previewUrl = "data:image/png,broken";
  const { screen } = await setup(page, library);
  await screen.open("?q=window");
  await screen.expectResults(3, 3);
  await screen.expectCardSource(library.items[0], false);
  await screen.expectCardSource(library.items[1], false);
  await screen.expectPlaceholder(library.items[1].title);
  await screen.expectPlaceholder(library.items[2].title);
});

test("Given a broken photographer preview, when results render, then its placeholder identifies the linked reference", async ({
  page,
}) => {
  const library = new SearchLibrary(1);
  library.items[0].referenceCount = 1;
  library.items[0].referencePreviewUrls = ["data:image/png,broken"];
  const { screen } = await setup(page, library);
  await screen.open("?q=window");
  await screen.expectResults(1, 1);
  await screen.expectPlaceholder(
    library.items[0].title,
    "Linked reference 1: no image",
  );
});

test("Given a successful empty search, when Edit query is chosen, then the existing query is focused for correction rather than offering service retry", async ({
  page,
}) => {
  const { library, screen } = await setup(page);
  await screen.open("?q=absent&type=references");
  await screen.expectEmpty();
  await screen.expectNoRetry();
  await screen.expectNoEmptyClear();
  await screen.editQuery();
  await screen.expectQuery("absent");
  expect(library.calls).toHaveLength(1);
  await screen.search("window");
  await screen.expectResults(20, 20);
  await screen.expectType("References");
});

test("Given an empty filtered search, when Clear filters is chosen in the results, then query and type remain while both filter groups are removed", async ({
  page,
}) => {
  const library = new SearchLibrary();
  const { screen } = await setup(page, library);
  await screen.open(
    `?q=window&type=photographers&tags=cinematic&boardIds=${library.boards[0].id}`,
  );
  await screen.expectBoardExplanation();
  await screen.clearEmptyFilters();
  await screen.expectResults(6, 6);
  await screen.expectQuery("window");
  await screen.expectType("Photographers");
  await screen.expectNoFilters();
  expect(library.calls.at(-1)).toMatchObject({
    query: "window",
    type: "photographers",
    tags: [],
    boardIds: [],
  });

  const params = new URL(page.url()).searchParams;
  expect(params.get("q")).toBe("window");
  expect(params.get("type")).toBe("photographers");
  expect(params.has("tags")).toBe(false);
  expect(params.has("boardIds")).toBe(false);
});

test("Given a later-page failure, when retry succeeds, then loaded cards and filters stay intact, the same cursor appends once, and paging stops at the end", async ({
  page,
}) => {
  const { library, screen } = await setup(page, new SearchLibrary(55));
  await screen.open("?q=window&tags=portrait");
  await screen.expectResults(24, 55);
  const url = page.url();
  library.failures = 1;
  await screen.more();
  await screen.expectError();
  await screen.expectCards(library.items.slice(0, 24));
  await screen.expectFilter("portrait");
  expect(page.url()).toBe(url);
  expect(library.calls.at(-1).cursor).toBe("24");
  await screen.retry();
  await screen.expectResults(48, 55);
  await screen.expectCards(library.items.slice(0, 48));
  expect(library.calls[2]).toEqual(library.calls[1]);
  await screen.more();
  await screen.expectResults(55, 55);
  await screen.expectCards(library.items);
  await screen.expectEnd();
  expect(library.calls.map((call) => call.cursor)).toEqual([
    undefined,
    "24",
    "24",
    "48",
  ]);
  expect(
    library.calls.every(
      (call) => call.query === "window" && call.tags.join() === "portrait",
    ),
  ).toBe(true);
});

test("Given an empty library, when a submitted browse is pending then succeeds, initial guidance, loading and successful no-data states remain distinct", async ({
  page,
}) => {
  const library = new SearchLibrary(0);
  const gate = library.deferNext();
  const { screen } = await setup(page, library);
  await screen.open();
  await screen.expectInitialOnly();
  expect(library.calls).toHaveLength(0);
  await screen.search("");
  await screen.expectLoading();
  await expect.poll(() => library.calls.length).toBe(1);
  gate.release();
  await screen.waitForResponses(1);
  await screen.expectSettled();
  await screen.expectEmpty();
  await screen.expectNoRetry();
  await screen.expectNoEmptyClear();
});

for (const outcome of ["success", "failure"]) {
  for (const change of ["query", "filters"]) {
    test(`Given delayed stale ${outcome}, when newer ${change} finish first, then the old snapshot cannot replace results or surface an error`, async ({
      page,
    }) => {
      const library = new SearchLibrary(30);
      const gate = library.deferNext(
        outcome === "failure"
          ? {
              error: "invalid_request",
              errors: { query: ["Stale query validation"] },
            }
          : undefined,
      );
      const { screen } = await setup(page, library);
      await screen.open("?q=window");
      await screen.expectLoading();
      await expect.poll(() => gate.captured !== null).toBe(true);
      library.items[1].title = "Newest result";
      if (change === "query") {
        await screen.search("newest");
        await screen.expectResults(1, 1);
      } else {
        await screen.filters();
        await screen.toggleTag("cinematic");
        await screen.applyFilters();
        await screen.expectResults(6, 6);
      }
      const url = page.url();
      const expected =
        change === "query"
          ? [library.items[1]]
          : library.items.filter((item) => item.tags.includes("cinematic"));
      gate.release();
      await screen.waitForResponses(2);
      await screen.expectCards(expected);
      await screen.expectNoError();
      await screen.expectQuery(change === "query" ? "newest" : "window");
      if (change === "filters") await screen.expectFilter("cinematic");
      expect(page.url()).toBe(url);
      expect(library.calls).toHaveLength(2);
    });
  }
  test(`Given a delayed ${outcome}, when the search route is destroyed, then late completion cannot resurrect results or errors`, async ({
    page,
  }) => {
    const library = new SearchLibrary(30);
    const gate = library.deferNext(
      outcome === "failure" ? { error: "request_failed" } : undefined,
    );
    const { screen } = await setup(page, library);
    const errors = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await screen.open("?q=window");
    await screen.expectLoading();
    await expect.poll(() => gate.captured !== null).toBe(true);
    await screen.leaveForMyWork();
    gate.release();
    await screen.waitForResponses(1);
    await screen.expectNoResults();
    expect(page.url()).toContain("/my-work");
    expect(errors).toEqual([]);
    await page.goBack();
    await screen.expectResults(24, 30);
    await screen.expectCards(library.items.slice(0, 24));
    expect(library.calls).toHaveLength(2);
  });
}

for (const action of ["Submit", "Load more", "Try again"]) {
  test(`Given ${action} is already pending, when activated repeatedly, then only one request runs for that page`, async ({
    page,
  }) => {
    const library = new SearchLibrary(30);
    if (action === "Try again") library.failures = 1;
    const { screen } = await setup(page, library);
    const first = action === "Submit" ? library.deferNext() : null;
    await screen.open("?q=window");
    if (action === "Try again") await screen.expectError();
    else if (action === "Load more") await screen.expectResults(24, 30);
    else await screen.expectLoading();
    const gate = first ?? library.deferNext();
    const expectedCalls = action === "Submit" ? 1 : 2;
    await screen.repeatAction(action);
    await expect.poll(() => gate.captured !== null).toBe(true);
    try {
      expect(library.calls).toHaveLength(expectedCalls);
    } finally {
      gate.release();
    }
    await screen.waitForResponses(expectedCalls);
    await screen.expectResults(action === "Load more" ? 30 : 24, 30);
    await screen.expectCards(
      library.items.slice(0, action === "Load more" ? 30 : 24),
    );
  });
}

for (const firstType of ["reference", "photographer"]) {
  test(`Given mixed results, when Load more succeeds, then the first added ${firstType} card gains focus in result order`, async ({
    page,
  }) => {
    const library = new SearchLibrary(30);
    if (firstType === "photographer") {
      [library.items[24], library.items[25]] = [
        library.items[25],
        library.items[24],
      ];
    }
    const { screen } = await setup(page, library);
    await screen.open("?q=window");
    await screen.expectResults(24, 30);
    await screen.more();
    await screen.expectResults(30, 30);
    await screen.expectCardFocused(library.items[24]);
  });
}

for (const change of ["query", "filters"]) {
  test(`Given typing on Search, when passive ${change} navigation and its delayed results complete, then focus and the unfinished draft stay in the query field`, async ({
    page,
  }) => {
    const { library, screen } = await setup(page, new SearchLibrary(30));
    await screen.open("?q=window");
    await screen.expectResults(24, 30);
    const gate = library.deferNext();
    if (change === "query") await screen.search("portrait 19");
    else {
      await screen.draftQuery("window");
      await screen.restoreUrl("?q=window&tags=cinematic");
    }
    await screen.expectLoading();
    await expect.poll(() => gate.captured !== null).toBe(true);
    try {
      await screen.expectQueryFocused();
      await screen.draftQuery("unfinished typing");
    } finally {
      gate.release();
    }
    await screen.waitForResponses(2);
    await screen.expectResults(
      change === "query" ? 1 : 6,
      change === "query" ? 1 : 6,
    );
    await screen.expectQueryFocused();
    await screen.expectQuery("unfinished typing");
  });
}

test("Given pending Load more, when the user starts an unsubmitted query, then completion preserves typing focus and the draft", async ({
  page,
}) => {
  const { library, screen } = await setup(page, new SearchLibrary(30));
  await screen.open("?q=window");
  await screen.expectResults(24, 30);
  const gate = library.deferNext();
  await screen.more();
  await expect.poll(() => gate.captured !== null).toBe(true);
  await screen.draftQuery("unfinished new query");
  gate.release();
  await screen.waitForResponses(2);
  await screen.expectResults(30, 30);
  await screen.expectQueryFocused();
  await screen.expectQuery("unfinished new query");
});

for (const count of [0, 1]) {
  test(`Given initial search failure, when Retry recovers ${count ? "a card" : "an empty result"}, then focus moves to the recovered content`, async ({
    page,
  }) => {
    const library = new SearchLibrary(count);
    library.failures = 1;
    const { screen } = await setup(page, library);
    await screen.open("?q=window");
    await screen.expectError();
    await screen.retry();
    if (count) {
      await screen.expectResults(1, 1);
      await screen.expectCardFocused(library.items[0]);
    } else {
      await screen.expectEmpty();
      await screen.expectEmptyFocused();
    }
  });
}

test("Given a later-page failure, when Retry appends the recovered page, then focus moves to its first card instead of earlier cards", async ({
  page,
}) => {
  const { library, screen } = await setup(page, new SearchLibrary(30));
  await screen.open("?q=window");
  await screen.expectResults(24, 30);
  library.failures = 1;
  await screen.more();
  await screen.expectError();
  await screen.retry();
  await screen.expectResults(30, 30);
  await screen.expectCardFocused(library.items[24]);
});

test("Given retained cards during pagination, when loading fails and Retry succeeds, then a single status distinguishes loading, retained results and completion", async ({
  page,
}) => {
  const { library, screen } = await setup(page, new SearchLibrary(30));
  await screen.open("?q=window");
  await screen.expectResults(24, 30);
  const gate = library.deferNext({ error: "request_failed" });
  await screen.more();
  await screen.expectLoading();
  try {
    await screen.expectStatus(
      "Loading more results… 24 of 30 results retained.",
    );
  } finally {
    gate.release();
  }
  await screen.expectError();
  await screen.expectStatus(
    "24 of 30 results retained. Results could not be updated.",
  );
  await screen.retry();
  await screen.expectResults(30, 30);
  await screen.expectStatus("Showing 30 of 30 results for “window”.");
  await screen.search("absent");
  await screen.expectEmpty();
  await screen.expectStatus("Showing 0 of 0 results for “absent”.");
});

test("Given an invalid pagination cursor, when Refresh results is explicitly chosen, then only paging resets while all search parameters and the URL remain intact", async ({
  page,
}) => {
  const library = new SearchLibrary(60);
  for (const item of library.items)
    if (item.type === "reference") item.boardIds = [library.boards[0].id];
  const { screen } = await setup(page, library);
  await screen.open(
    `?q=window&type=references&tags=portrait&boardIds=${library.boards[0].id}&mode=keyword`,
  );
  await screen.expectResults(24, 48);
  const url = page.url();
  const original = structuredClone(library.calls[0]);
  library.errors.push({
    error: "invalid_request",
    errors: { cursor: ["The continuation is no longer valid."] },
  });
  await screen.more();
  await screen.expectCursorRefresh();
  await screen.expectCards(
    library.items.filter((item) => item.type === "reference").slice(0, 24),
  );
  await screen.expectFilter("portrait");
  await screen.expectFilter("Portrait study");
  expect(page.url()).toBe(url);
  expect(library.calls).toHaveLength(2);
  expect(library.calls[1]).toEqual({ ...original, cursor: "24" });
  const refreshed = library.items.pop();
  refreshed.title = "Window refreshed";
  library.items.unshift(refreshed);
  const gate = library.deferNext();
  await screen.refreshResults();
  await screen.expectLoading();
  await screen.expectCards([]);
  await expect.poll(() => library.calls.length).toBe(3);
  expect(library.calls[2]).toEqual(original);
  gate.release();
  await screen.waitForResponses(3);
  await screen.expectResults(24, 48);
  await screen.expectCards(
    library.items.filter((item) => item.type === "reference").slice(0, 24),
  );
  await screen.expectCardFocused(refreshed);
  await screen.expectQuery("window");
  await screen.expectType("References");
  expect(page.url()).toBe(url);
});

for (const outcome of ["success", "failure"]) {
  test(`Given delayed pagination ${outcome}, when a newer query completes, then late pagination cannot append cards, announce errors or steal typing focus`, async ({
    page,
  }) => {
    const { library, screen } = await setup(page, new SearchLibrary(30));
    await screen.open("?q=window");
    await screen.expectResults(24, 30);
    const gate = library.deferNext(
      outcome === "failure" ? { error: "request_failed" } : undefined,
    );
    await screen.more();
    await screen.expectLoading();
    await expect.poll(() => gate.captured !== null).toBe(true);
    await screen.search("portrait 19");
    await screen.expectResults(1, 1);
    await screen.draftQuery("still typing");
    gate.release();
    await screen.waitForResponses(3);
    await screen.expectCards([library.items[19]]);
    await screen.expectNoError();
    await screen.expectQuery("still typing");
    await screen.expectQueryFocused();
    await screen.expectStatus("Showing 1 of 1 results for “portrait 19”.");
  });
}
