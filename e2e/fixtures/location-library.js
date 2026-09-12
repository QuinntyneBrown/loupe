const coverUrl =
  "data:image/svg+xml," +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="480" height="600"><rect width="480" height="600" fill="#d8d3c8"/><path d="M0 600 300 0h80L80 600" fill="#8a8378"/></svg>',
  );

import { ScoutingReports } from "./scouting-reports.js";

export class LocationLibrary {
  constructor(count = 7) {
    this.failures = { list: 0, get: 0, create: 0, update: 0, updateText: 0, setTags: 0, deleteLocation: 0, addImage: 0, removeImage: 0, setCover: 0 };
    this.errors = { create: [], update: [], updateText: [], setTags: [], deleteLocation: [], addImage: [], removeImage: [], setCover: [] };
    this.aborted = [];
    this.deletions = [];
    this.gates = {};
    this.calls = [];
    this.receipts = new Map();
    this.coverUrl = coverUrl;
    this.items = Array.from({ length: count }, (_, index) =>
      LocationLibrary.location(index),
    );
    this.scouting = new ScoutingReports(this);
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
      scoutingBrief: number % 2 ? "Couple sessions at low tide, two people, nothing staged." : null,
      notes: number % 2 ? "Parking on Kew Green, five minutes' walk." : null,
      tags: number % 2
        ? [
            { name: "river", category: "subject" },
            { name: "low tide", category: null },
          ]
        : [],
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
  create(input) {
    if (this.receipts.has(input.operationKey))
      return { data: this.receipts.get(input.operationKey) };
    const errors = LocationLibrary.validate(input);
    if (Object.keys(errors).length) return { error: "invalid_request", errors };
    const item = {
      ...LocationLibrary.location(this.items.length),
      id: `created-${this.items.length + 1}`,
      name: input.name.trim(),
      addressLine1: input.addressLine1?.trim() || null,
      addressLine2: input.addressLine2?.trim() || null,
      locality: input.locality?.trim() || null,
      region: input.region?.trim() || null,
      postalCode: input.postalCode?.trim() || null,
      country: input.country?.trim() || null,
      coordinates: input.coordinates
        ? {
            latitude: Number(input.coordinates.latitude).toFixed(6),
            longitude: Number(input.coordinates.longitude).toFixed(6),
          }
        : null,
      setting: input.setting || null,
      scoutingBrief: input.scoutingBrief?.trim() || null,
      notes: input.notes?.trim() || null,
      tags: input.tags ?? [],
      images: [],
      coverImageId: null,
      reportStatus: "None",
      revision: 1,
    };
    this.items.unshift(item);
    this.receipts.set(input.operationKey, item);
    return { data: item };
  }
  addImage(input) {
    if (this.receipts.has(input.operationKey)) return { data: this.receipts.get(input.operationKey) };
    const item = this.items.find((item) => item.id === input.id);
    if (!item) return { error: "item_unavailable" };
    if (!["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"].includes(input.contentType))
      return { error: "unsupported_media" };
    if (item.images.length >= 10) return { error: "invalid_request", errors: { images: ["A location holds up to 10 images."] } };
    const image = {
      id: `${item.id}-image-${item.images.length + 1}-${input.filename}`,
      position: item.images.length + 1,
      imageUrl: coverUrl,
      previewUrl: coverUrl,
      width: 480,
      height: 600,
    };
    item.images = [...item.images, image];
    item.coverImageId ??= image.id;
    item.revision += 1;
    item.updatedAt = "2026-09-13T09:30:00Z";
    this.receipts.set(input.operationKey, item);
    return { data: item };
  }
  static validate(input) {
    const errors = {};
    if (!input.name?.trim()) errors.name = ["Enter a name."];
    else if ([...input.name.trim()].length > 200)
      errors.name = ["Use 200 characters or fewer."];
    const latitude = input.coordinates?.latitude?.trim() ?? "";
    const longitude = input.coordinates?.longitude?.trim() ?? "";
    if (latitude && !longitude)
      errors.longitude = ["Enter a longitude to go with the latitude."];
    if (longitude && !latitude)
      errors.latitude = ["Enter a latitude to go with the longitude."];
    if (!input.operationKey) errors.operationKey = ["Provide an Idempotency-Key."];
    return errors;
  }
  hold(operation, operationKey) {
    let release;
    const promise = new Promise((resolve) => (release = resolve));
    this.gates[operationKey ? operation + ":" + operationKey : operation] = { promise, release };
    return release;
  }
  async reportProgress(page, operationKey, transferred, total) {
    await page.evaluate(
      (detail) => window.dispatchEvent(new CustomEvent("loupe-location-upload-progress", { detail })),
      { operationKey, transferred, total },
    );
  }
  async attach(page) {
    await this.scouting.attach(page);
    await page.exposeFunction("loupeLocations", async (operation, input) => {
      this.calls.push({ operation, ...input });
      if (operation === "abortUpload") {
        this.aborted.push(input.operationKey);
        return { data: null };
      }
      await (this.gates[operation + ":" + input.operationKey] ?? this.gates[operation])?.promise;
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
      const custom = this.errors[operation]?.shift();
      if (custom) return custom;
      if (operation === "create") return this.create(input);
      if (operation === "get") {
        const item = this.items.find((item) => item.id === input.id);
        return item ? { data: item } : { error: "item_unavailable" };
      }
      if (operation === "addImage") return this.addImage(input);
      if (["update", "updateText", "setTags", "deleteLocation", "removeImage", "setCover"].includes(operation)) {
        const item = this.items.find((item) => item.id === input.id);
        if (!item) return { error: "item_unavailable" };
        if (item.revision !== input.revision) return { error: "revision_conflict" };
        if (operation === "update") {
          const errors = LocationLibrary.validate({ ...input, operationKey: "edit" });
          if (Object.keys(errors).length) return { error: "invalid_request", errors };
          Object.assign(item, {
            name: input.name.trim(),
            addressLine1: input.addressLine1?.trim() || null,
            addressLine2: input.addressLine2?.trim() || null,
            locality: input.locality?.trim() || null,
            region: input.region?.trim() || null,
            postalCode: input.postalCode?.trim() || null,
            country: input.country?.trim() || null,
            coordinates: input.coordinates
              ? {
                  latitude: Number(input.coordinates.latitude).toFixed(6),
                  longitude: Number(input.coordinates.longitude).toFixed(6),
                }
              : null,
            setting: input.setting || null,
          });
        } else if (operation === "updateText") item[input.field] = input.text?.trim() || null;
        else if (operation === "setTags") item.tags = input.tags.map((tag) => ({ name: tag.name, category: tag.category ?? null }));
        else if (operation === "setCover") {
          if (!item.images.some((image) => image.id === input.imageId)) return { error: "item_unavailable" };
          item.coverImageId = input.imageId;
        } else if (operation === "removeImage") {
          const removed = item.images.find((image) => image.id === input.imageId);
          if (!removed) return { error: "item_unavailable" };
          item.images = item.images.filter((image) => image.id !== input.imageId).map((image, index) => ({ ...image, position: index + 1 }));
          if (item.coverImageId === removed.id)
            item.coverImageId = (item.images.find((image) => image.position >= removed.position) ?? item.images[0])?.id ?? null;
          this.removedImages = [...(this.removedImages ?? []), removed.id];
        } else {
          this.items = this.items.filter((other) => other.id !== item.id);
          this.deletions.push(item.id);
          return {
            data: {
              id: `deletion-${this.deletions.length}`,
              resourceId: item.id,
              status: "Pending",
              deletedAt: "2026-09-12T12:00:00Z",
              completedAt: null,
            },
          };
        }
        item.revision += 1;
        item.updatedAt = "2026-09-13T09:30:00Z";
        return { data: item };
      }
      throw new Error("Unexpected location operation: " + operation);
    });
  }
}
