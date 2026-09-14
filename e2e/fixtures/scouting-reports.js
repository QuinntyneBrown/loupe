const shootTypes = ["Portraits", "Family portraits", "Headshots", "Engagement", "Events"];
const periods = ["Dawn", "Morning", "Midday", "Afternoon", "Golden hour", "Blue hour", "Night"];

export class ScoutingReports {
  constructor(library) {
    this.library = library;
    this.operations = new Map();
    this.calls = [];
    this.errors = { request: [], retry: [] };
    this.failures = { current: 0, get: 0 };
    this.receipts = new Map();
    this.counter = 0;
  }
  /** A complete report over the location's current images, citing the given 1-based image numbers. */
  report(item, options = {}) {
    const cited = (numbers) => numbers.map((number) => item.images[number - 1]?.id ?? "missing");
    const entry = (fields, basis, numbers) => ({ ...fields, basis, citedImageIds: cited(numbers) });
    const imageCount = options.imageCount ?? item.images.length;
    return {
      operationId: options.operationId ?? `scouting-${item.id}`,
      generatedAt: options.generatedAt ?? "2026-09-10T09:30:00Z",
      mode: "Live",
      model: "gpt-4.1",
      promptVersion: "location-scouting-v1",
      briefSnapshot: options.briefSnapshot ?? item.scoutingBrief,
      imageSetRevision: options.imageSetRevision ?? 1,
      imageCount,
      report: {
        overview: [
          entry({ strength: "Repeating arches give depth", reason: "The bridge arches recede in a line across the images." }, "Visible", [1, 3]),
          entry({ strength: "Open shade under the bridge", reason: "Even, directionless light under the arch while the bank is in full sun." }, "Visible", [3]),
        ],
        suitability: shootTypes.map((shootType, index) =>
          entry(
            {
              shootType,
              rating: index === 1 ? "Not recommended" : index === 4 ? "Workable" : "Well suited",
              reason: index === 1 ? "The gravel is uneven and the water is close." : "Open shade for faces and a long empty bank.",
            },
            "Visible",
            [1],
          ),
        ),
        timesOfDay: periods.map((period) =>
          entry(
            {
              period,
              rating: period === "Golden hour" || period === "Morning" ? "Recommended" : period === "Midday" ? "Avoid" : "Unknown",
              reason: period === "Midday" ? "Bare gravel reflects hard overhead light." : period === "Golden hour" || period === "Morning" ? "Open sky lights the arches from the side." : "The images give no basis for this period.",
            },
            period === "Golden hour" || period === "Morning" || period === "Midday" ? "Visible" : "Inferred",
            [1],
          ),
        ),
        techniques: [
          entry({ technique: "Leading lines", explanation: "Use the arches and the water's edge to lead into the subject." }, "Visible", [1]),
          entry({ technique: "Natural framing", explanation: "Frame a couple inside the nearest arch." }, "Visible", [3]),
        ],
        groupSize: entry({ cannotAssess: false, minimum: 2, maximum: 8, reason: "The dry gravel strip fits a small group." }, "Visible", [1, 3]),
        cautions: [entry({ caution: "Wet stones near the waterline are slippery." }, "Visible", [2])],
      },
    };
  }
  operation(item, status = "Queued", extra = {}) {
    const operation = {
      id: `scouting-operation-${++this.counter}`,
      resourceId: item.id,
      type: "LocationScouting",
      status,
      mode: "Live",
      createdAt: "2026-09-12T12:00:00Z",
      updatedAt: "2026-09-12T12:00:00Z",
      completedAt: null,
      nextAttemptAt: null,
      retryAvailableAt: null,
      failureCode: null,
      message: status === "Queued" ? "Waiting to start." : "Processing saved content.",
      ...extra,
    };
    this.operations.set(item.id, operation);
    item.reportStatus = status === "Succeeded" ? "Ready" : status;
    return operation;
  }
  /** Seeds a completed report; the status follows the image set unless overridden. */
  ready(item, options = {}) {
    item.report = this.report(item, options);
    item.reportStatus = options.status ?? "Ready";
    return item.report;
  }
  advance(item, status = "Running") {
    const operation = this.operations.get(item.id);
    operation.status = status;
    operation.message = status === "Running" ? "Looking at the images." : operation.message;
    item.reportStatus = status;
  }
  complete(item, options = {}) {
    const operation = this.operations.get(item.id);
    operation.status = "Succeeded";
    operation.completedAt = "2026-09-12T12:01:00Z";
    item.report = this.report(item, { operationId: operation.id, generatedAt: "2026-09-12T12:01:00Z", ...options });
    item.reportStatus = "Ready";
    item.revision += 1;
  }
  fail(item, failureCode = "provider_unavailable", message = "The analysis service could not complete this request. Your saved content is unchanged.") {
    const operation = this.operations.get(item.id) ?? this.operation(item);
    operation.status = "Failed";
    operation.failureCode = failureCode;
    operation.message = message;
    operation.completedAt = "2026-09-12T12:02:00Z";
    item.reportStatus = "Failed";
  }
  async attach(page) {
    await page.exposeFunction("loupeScoutingReports", async (operation, input) => {
      this.calls.push({ operation, ...input });
      if (this.failures[operation] > 0) {
        this.failures[operation]--;
        return { error: "request_failed" };
      }
      const custom = this.errors[operation]?.shift();
      if (custom) return custom;
      const item = this.library.items.find((item) => item.id === (input.id ?? this.operationOwner(input.operationId)));
      if (!item) return { error: "item_unavailable" };
      if (operation === "current") return { data: this.operations.get(item.id) ?? null };
      if (operation === "get") return { data: item.report ?? null };
      if (operation === "request") {
        if (this.receipts.has(input.operationKey)) return { data: this.receipts.get(input.operationKey) };
        if (item.revision !== input.revision) return { error: "revision_conflict" };
        if (!item.images.length) return { error: "invalid_request", errors: { images: ["Add an image before requesting a scouting report."] } };
        const active = this.operations.get(item.id);
        if (active && (active.status === "Queued" || active.status === "Running")) return { data: active };
        if (!input.regenerate && active?.status === "Succeeded" && item.report) return { data: active };
        const created = this.operation(item);
        this.receipts.set(input.operationKey, created);
        return { data: created };
      }
      if (operation === "retry") {
        const failed = this.operations.get(item.id);
        if (!failed || failed.status !== "Failed") return { error: "retry_unavailable" };
        if (item.revision !== input.revision) return { error: "revision_conflict" };
        return { data: this.operation(item) };
      }
      throw new Error("Unexpected scouting operation: " + operation);
    });
  }
  operationOwner(operationId) {
    for (const [locationId, operation] of this.operations) if (operation.id === operationId) return locationId;
    return null;
  }
}
