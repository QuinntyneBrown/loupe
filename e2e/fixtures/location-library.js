const coverUrl =
  "data:image/svg+xml," +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="480" height="600"><rect width="480" height="600" fill="#d8d3c8"/><path d="M0 600 300 0h80L80 600" fill="#8a8378"/></svg>',
  );

export class LocationLibrary {
  constructor(count = 7) {
    this.failures = { list: 0, get: 0, create: 0 };
    this.gates = {};
    this.calls = [];
    this.coverUrl = coverUrl;
    this.items = Array.from({ length: count }, (_, index) =>
      LocationLibrary.location(index),
    );
  }
  static location(index) {
    const number = index + 1;
    const imageCount = number % 4;
    const images = Array.from({ length: imageCount }, (_, position) => ({
      id: `image-${number}-${position + 1}`,
      position: position + 1,
      imageUrl: coverUrl,
      previewUrl: coverUrl,
      width: 480,
      height: 600,
    }));
    return {
      id: `00000000-0000-4000-9000-${String(number).padStart(12, "0")}`,
      name: `Location ${String(number).padStart(2, "0")}`,
      addressLine1: number % 2 ? "1 Riverside Walk" : null,
      addressLine2: null,
      locality: number % 2 ? "Richmond" : null,
      region: number % 2 ? "Greater London" : null,
      postalCode: null,
      country: number % 2 ? "United Kingdom" : null,
      coordinates: number % 2
        ? { latitude: "51.487213", longitude: "-0.287604" }
        : null,
      setting: number % 2 ? "Outdoor" : null,
      scoutingBrief: null,
      notes: null,
      tags: [],
      images,
      coverImageId: images[0]?.id ?? null,
      report: null,
      reportStatus: number % 5 === 0 && imageCount ? "Ready" : "None",
      createdAt: "2026-09-12T12:00:00Z",
      updatedAt: "2026-09-12T12:00:00Z",
      revision: 1,
    };
  }
  static summary(item) {
    const cover = item.images.find((image) => image.id === item.coverImageId);
    return {
      id: item.id,
      name: item.name,
      locality: item.locality,
      coverPreviewUrl: cover?.previewUrl ?? null,
      imageCount: item.images.length,
      reportStatus: item.reportStatus,
      createdAt: item.createdAt,
    };
  }
  hold(operation) {
    let release;
    const promise = new Promise((resolve) => (release = resolve));
    this.gates[operation] = { promise, release };
    return release;
  }
  async attach(page) {
    await page.exposeFunction("loupeLocations", async (operation, input) => {
      this.calls.push({ operation, ...input });
      await this.gates[operation]?.promise;
      if (this.failures[operation] > 0) {
        this.failures[operation]--;
        return { error: "request_failed" };
      }
      if (operation === "list") {
        const start = Number(input.cursor ?? 0);
        const end = Math.min(this.items.length, start + 24);
        return {
          data: {
            items: this.items.slice(start, end).map(LocationLibrary.summary),
            nextCursor: end < this.items.length ? String(end) : null,
            totalCount: this.items.length,
          },
        };
      }
      throw new Error("Unexpected location operation: " + operation);
    });
  }
}
