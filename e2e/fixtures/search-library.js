import { ReferenceLibrary } from "./reference-library.js";
import { PhotographerLibrary } from "./photographer-library.js";

export class SearchLibrary {
  constructor(count = 26) {
    this.calls = [];
    this.gates = [];
    this.failures = 0;
    this.errors = [];
    this.tagCalls = 0;
    this.tagSelections = [];
    this.boardCalls = 0;
    this.tagFailures = 0;
    this.boardFailures = 0;
    this.tagIdentities = new Map([
      ["cinematic", "CINEMATIC"],
      ["portrait", "PORTRAIT"],
      ["PORTRAIT", "PORTRAIT"],
      [" PORTRAIT ", "PORTRAIT"],
      ["soft light", "SOFT LIGHT"],
      ...Array.from({ length: 11 }, (_, index) => [
        `study ${index + 1}`,
        `STUDY ${index + 1}`,
      ]),
    ]);
    this.boards = Array.from({ length: 11 }, (_, index) => ({
      id: `30000000-0000-4000-8000-${String(index + 1).padStart(12, "0")}`,
      name:
        index === 0
          ? "Portrait study"
          : index === 1
            ? "Window light"
            : `Board ${index + 1}`,
      revision: 1,
      referenceCount: 0,
    }));
    this.items = Array.from({ length: count }, (_, index) => ({
      id: `${index % 5 === 0 ? "20000000" : "10000000"}-0000-4000-8000-${String(index + 1).padStart(12, "0")}`,
      type: index % 5 === 0 ? "photographer" : "reference",
      title: `Window portrait ${index}`,
      description: "Portraits in soft window light.",
      attribution: "Casey",
      createdAt: "2026-09-11T12:00:00Z",
      previewUrl: null,
      sourceUrl: `https://portfolio.example/${index}`,
      width: null,
      height: null,
      referenceCount: index,
      referencePreviewUrls: [],
      tags:
        index % 5 === 0
          ? ["cinematic", "portrait"]
          : [
              "portrait",
              ...(index % 2 === 0 ? ["soft light"] : []),
              `study ${(index % 11) + 1}`,
            ],
      boardIds: index % 5 === 0 ? [] : [this.boards[index % 3].id],
    }));
  }
  deferNext(response) {
    let release;
    const promise = new Promise((resolve) => {
      release = resolve;
    });
    const gate = { promise, release, response, captured: null };
    this.gates.push(gate);
    return gate;
  }
  tagIdentity(name) {
    return this.tagIdentities.get(name) ?? name;
  }
  async attach(page, { details = false } = {}) {
    if (details) {
      this.references = new ReferenceLibrary(0);
      this.photographers = new PhotographerLibrary(0);
      const reference = new ReferenceLibrary(1).items[0];
      const photographer = new PhotographerLibrary(1).items[0];
      this.references.items = this.items
        .filter((item) => item.type === "reference")
        .map((item) => ({
          ...reference,
          ...item,
          imageUrl: item.previewUrl,
          tags: item.tags.map((name) => ({
            name,
            category: "genre",
            provenance: "manual",
          })),
        }));
      this.photographers.items = this.items
        .filter((item) => item.type === "photographer")
        .map((item) => ({
          ...photographer,
          id: item.id,
          name: item.title,
          portfolioUrl: item.sourceUrl,
          summary: item.description,
          tags: item.tags.map((name) => ({
            name,
            category: "genre",
            provenance: "manual",
          })),
          referenceCount: item.referenceCount,
          references: [],
        }));
      this.references.boards = this.boards;
      this.references.photographerLibrary = this.photographers;
      this.photographers.referenceLibrary = this.references;
      await this.references.attach(page);
      await this.photographers.attach(page);
    }
    await page.exposeFunction("loupeSearchTags", async (selectedTags = []) => {
      this.tagCalls++;
      this.tagSelections.push(selectedTags);
      if (this.tagFailures > 0) {
        this.tagFailures--;
        return { error: "request_failed" };
      }
      if (
        selectedTags.length > 10 ||
        selectedTags.some((tag) => {
          const length = Array.from(tag.normalize("NFC").trim()).length;
          return length === 0 || length > 50 || tag.includes("\0");
        })
      )
        return {
          error: "invalid_request",
          errors: { selectedTags: ["Choose up to 10 valid tag names."] },
        };
      const facets = new Map();
      for (const item of this.items)
        for (const normalizedName of new Set(
          item.tags.map((tag) => this.tagIdentity(tag)),
        )) {
          const facet = facets.get(normalizedName) ?? {
            name: item.tags.find(
              (tag) => this.tagIdentity(tag) === normalizedName,
            ),
            normalizedName,
            count: 0,
            selectedNames: selectedTags.filter(
              (tag) => this.tagIdentity(tag) === normalizedName,
            ),
          };
          facet.count++;
          facets.set(normalizedName, facet);
        }
      return {
        data: [...facets.values()].sort((a, b) =>
          a.normalizedName.localeCompare(b.normalizedName),
        ),
      };
    });
    if (!details)
      await page.exposeFunction("loupeBoards", async (operation) => {
        if (operation !== "list")
          throw new Error(`Unexpected search board operation: ${operation}`);
        this.boardCalls++;
        if (this.boardFailures > 0) {
          this.boardFailures--;
          return { error: "request_failed" };
        }
        return {
          data: this.boards.map((board) => ({
            ...board,
            referenceCount: this.items.filter((item) =>
              item.boardIds.includes(board.id),
            ).length,
          })),
        };
      });
    await page.exposeFunction("loupeSearch", async (input) => {
      this.calls.push(structuredClone(input));
      const gate = this.gates.shift();
      const response = structuredClone(
        gate?.response ?? this.searchResponse(input),
      );
      if (gate) {
        gate.captured = { input: structuredClone(input), response };
        await gate.promise;
      }
      return response;
    });
    await page.addInitScript(() => {
      const search = window.loupeSearch;
      window.loupeSearch = async (input) => {
        try {
          return await search(input);
        } finally {
          setTimeout(() => {
            window.loupeSearchSettled = (window.loupeSearchSettled ?? 0) + 1;
          }, 0);
        }
      };
    });
  }
  searchResponse(input) {
    if (this.failures > 0) {
      this.failures--;
      return { error: "request_failed" };
    }
    if (this.errors.length) return this.errors.shift();
    if (input.mode !== undefined && input.mode !== "keyword")
      return {
        error: "invalid_request",
        errors: { mode: ["Only keyword search is supported."] },
      };
    if (
      input.boardIds.some(
        (id) =>
          !this.boards.some(
            (board) => board.id.toLowerCase() === id.toLowerCase(),
          ),
      )
    )
      return { error: "item_unavailable" };
    const tokens = input.query
      .toLowerCase()
      .trim()
      .split(/\s+/)
      .filter(Boolean);
    const found = this.items.filter(
      (item) =>
        (input.type === "all" || input.type === item.type + "s") &&
        tokens.every((token) =>
          (
            item.title +
            " " +
            item.description +
            " " +
            item.attribution +
            " " +
            item.tags.join(" ")
          )
            .toLowerCase()
            .includes(token),
        ) &&
        input.tags.every((tag) =>
          item.tags.some(
            (value) => this.tagIdentity(value) === this.tagIdentity(tag),
          ),
        ) &&
        (!input.boardIds.length ||
          (item.type === "reference" &&
            input.boardIds.some((id) =>
              item.boardIds.some(
                (value) => value.toLowerCase() === id.toLowerCase(),
              ),
            ))),
    );
    const start = Number(input.cursor || 0),
      items = found.slice(start, start + 24);
    return {
      data: {
        items,
        totalCount: found.length,
        nextCursor:
          start + items.length < found.length
            ? String(start + items.length)
            : null,
      },
    };
  }
}
