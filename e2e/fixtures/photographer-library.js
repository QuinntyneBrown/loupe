import { PhotographerDrafts } from "./photographer-drafts.js";
export class PhotographerLibrary {
  constructor(count = 7) {
    this.items = Array.from({ length: count }, (_, index) => ({
      id: `photographer-${index + 1}`,
      name: `Photographer ${String(index + 1).padStart(2, "0")}`,
      portfolioUrl: `https://portfolio${index + 1}.example/work`,
      createdAt: "2026-08-12T12:00:00Z",
      summary:
        "Portraits in available light, mostly north-facing windows and long sittings. A restrained warm palette and almost no fill.",
      tags: [
        { name: "window light", category: "lighting", provenance: "manual" },
        { name: "portrait", category: "genre", provenance: "manual" },
      ],
      referenceCount: index ? 0 : 6,
      references: [],
      revision: 1,
      notes: "Study the window light.",
      summaryProvenance: "manual",
      sourceRevision: 1,
      sourceIsCurrent: true,
      sourceFailureCode: null,
      source: {
        requestedUrl: `https://portfolio${index + 1}.example/work`,
        fetchedUrl: `https://portfolio${index + 1}.example/work`,
        retrievedAt: "2026-08-12T12:00:00Z",
        title: "Portfolio",
        description: null,
        mainText: "Portfolio page",
        tags: [],
      },
    }));
    this.calls = [];
    this.failures = 0;
    this.drafts = new PhotographerDrafts(this);
    this.linked = [];
  }
  async attach(page) {
    await this.drafts.attach(page);
    await page.exposeFunction(
      "loupePhotographers",
      async (operation, input) => {
        this.calls.push({ operation, ...input });
        if (this.failures > 0) {
          this.failures--;
          return { error: "request_failed" };
        }
        if (operation === "list") {
          const offset = Number(input.cursor || 0);
          return {
            data: {
              items: this.items.slice(offset, offset + 24),
              nextCursor:
                offset + 24 < this.items.length ? String(offset + 24) : null,
              totalCount: this.items.length,
            },
          };
        }
        if (operation === "deletePhotographer") {
          const item = this.items.find((item) => item.id === input.id);
          if (!item) return { error: "item_unavailable" };
          if (item.revision !== input.revision)
            return { error: "revision_conflict" };
          this.items = this.items.filter((item) => item.id !== input.id);
          this.retainedReferences = [...this.linked];
          this.linked = [];
          return {
            data: {
              id: "deletion-1",
              resourceId: input.id,
              status: "Completed",
              deletedAt: "2026-09-11T12:00:00Z",
              completedAt: "2026-09-11T12:00:00Z",
            },
          };
        }
        if (operation === "update") {
          const item = this.items.find((item) => item.id === input.id);
          if (!item) return { error: "item_unavailable" };
          if (item.revision !== input.revision)
            return { error: "revision_conflict" };
          const changed = item.portfolioUrl !== input.portfolioUrl;
          Object.assign(item, input, {
            revision: item.revision + 1,
            sourceIsCurrent: changed ? false : item.sourceIsCurrent,
            sourceRevision: changed
              ? item.sourceRevision + 1
              : item.sourceRevision,
          });
          return { data: item };
        }
        if (operation === "get")
          return this.items.some((item) => item.id === input.id)
            ? { data: this.items.find((item) => item.id === input.id) }
            : { error: "item_unavailable" };
        if (operation === "references") {
          const offset = Number(input.cursor || 0);
          return {
            data: {
              items: this.linked.slice(offset, offset + 24),
              nextCursor:
                offset + 24 < this.linked.length ? String(offset + 24) : null,
              totalCount: this.linked.length,
            },
          };
        }
        throw new Error("Unexpected photographer operation: " + operation);
      },
    );
  }
}
