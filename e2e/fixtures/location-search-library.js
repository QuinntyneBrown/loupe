import { LocationLibrary } from "./location-library.js";

const seeds = [
  { name: "Kew Bridge foreshore", locality: "Richmond", images: 4, setting: "Outdoor", tags: ["river", "arches"], brief: "Couples at low tide under the arches.", report: { recommended: ["Morning", "Golden hour"], group: [2, 8], ratings: { Engagement: "Well suited", Events: "Workable" } } },
  { name: "Walthamstow wetlands", locality: "Waltham Forest", images: 4, setting: "Outdoor", tags: ["reeds", "open sky"], report: { recommended: ["Dawn", "Golden hour"], group: [2, 20], ratings: { Engagement: "Well suited", Events: "Well suited" } } },
  { name: "Hampstead ponds path", locality: "Camden", images: 3, setting: "Outdoor", tags: ["water", "path"], report: { recommended: ["Morning", "Golden hour"], group: [1, 6], ratings: { Engagement: "Well suited", Events: "Workable", "Family portraits": "Not recommended" } } },
  { name: "Peckham multi-storey roof", locality: "Southwark", images: 5, setting: "Outdoor", tags: ["rooftop", "skyline"], report: { recommended: ["Golden hour", "Blue hour"], group: [2, 40], ratings: { Engagement: "Workable", Events: "Well suited" } } },
  { name: "St Dunstan in the East", locality: "City of London", images: 6, setting: "Mixed", tags: ["ruin", "arches"], report: { recommended: ["Midday", "Golden hour"], group: [2, 12], ratings: { Engagement: "Well suited", Events: "Workable" } } },
  { name: "Bermondsey wall", locality: "Southwark", images: 5, setting: "Outdoor", tags: ["brick"], reportStatus: "Outdated", report: { recommended: ["Golden hour"], group: [2, 10], ratings: { Engagement: "Workable", Events: "Workable" } } },
  { name: "Deptford creek stairs", locality: "Lewisham", images: 1, setting: "Outdoor", tags: ["river", "stairs", "low tide"], notes: "The stairs flood; low tide only.", report: null },
  { name: "Cornmill Gardens bandstand", locality: "Lewisham", images: 3, setting: "Outdoor", tags: ["bandstand"], report: { recommended: ["Afternoon", "Golden hour"], group: null, ratings: { Engagement: "Workable", Events: "Well suited" } } },
  { name: "Window-lit studio", locality: null, images: 2, setting: "Indoor", tags: ["window light"], report: { recommended: ["Morning"], group: [1, 2], ratings: { Portraits: "Well suited", Headshots: "Well suited", Engagement: "Not recommended", Events: "Not recommended" } } },
  { name: "Empty lot", locality: null, images: 0, setting: null, tags: [], report: null },
];

/** Ten named locations with reports shaped for the shoot filters; detail pages come from the wrapped LocationLibrary. */
export class LocationSearchLibrary {
  constructor(count = seeds.length) {
    this.locations = new LocationLibrary(count);
    this.calls = [];
    this.failures = { search: 0, tags: 0 };
    this.errors = { search: [], tags: [] };
    this.gates = {};
    this.locations.items.forEach((item, index) => this.seed(item, seeds[index % seeds.length], index));
    this.items = this.locations.items;
  }
  seed(item, seed, index) {
    const images = Array.from({ length: seed.images }, (_, position) => ({
      id: `image-${index + 1}-${position + 1}`,
      position: position + 1,
      imageUrl: this.locations.coverUrl,
      previewUrl: this.locations.coverUrl,
      width: 480,
      height: 600,
    }));
    Object.assign(item, {
      name: seed.name,
      locality: seed.locality,
      addressLine1: null,
      region: null,
      country: null,
      coordinates: null,
      setting: seed.setting,
      scoutingBrief: seed.brief ?? null,
      notes: seed.notes ?? null,
      tags: seed.tags.map((name) => ({ name, category: null })),
      images,
      coverImageId: images[0]?.id ?? null,
      reportStatus: seed.report ? (seed.reportStatus ?? "Ready") : "None",
      report: null,
    });
    if (!seed.report) return;
    const report = this.locations.scouting.report(item, { imageCount: images.length });
    for (const entry of report.report.suitability) entry.rating = seed.report.ratings[entry.shootType] ?? "Cannot assess";
    for (const entry of report.report.timesOfDay) entry.rating = seed.report.recommended.includes(entry.period) ? "Recommended" : "Unknown";
    Object.assign(report.report.groupSize, seed.report.group
      ? { cannotAssess: false, minimum: seed.report.group[0], maximum: seed.report.group[1] }
      : { cannotAssess: true, minimum: null, maximum: null });
    item.report = report;
  }
  static searchItem(item) {
    const report = item.report?.report;
    return {
      id: item.id,
      name: item.name,
      locality: item.locality,
      coverPreviewUrl: item.images.find((image) => image.id === item.coverImageId)?.previewUrl ?? null,
      imageCount: item.images.length,
      reportStatus: item.reportStatus,
      recommendedPeriods: report ? report.timesOfDay.filter((entry) => entry.rating === "Recommended").map((entry) => entry.period) : [],
      groupSize: report ? { cannotAssess: report.groupSize.cannotAssess, minimum: report.groupSize.minimum, maximum: report.groupSize.maximum } : null,
      suitability: report ? report.suitability.map(({ shootType, rating }) => ({ shootType, rating })) : [],
      createdAt: item.createdAt,
    };
  }
  static text(item) {
    const strings = [];
    const walk = (value) => {
      if (typeof value === "string") strings.push(value);
      else if (value && typeof value === "object") Object.values(value).forEach(walk);
    };
    walk(item.report?.report ?? {});
    return [item.name, item.addressLine1, item.addressLine2, item.locality, item.region, item.postalCode, item.country, item.setting, item.notes, item.scoutingBrief, ...item.tags.map((tag) => tag.name), ...strings]
      .filter(Boolean)
      .join("\n")
      .normalize("NFC")
      .toLowerCase();
  }
  matches(item, request) {
    const tokens = (request.query ?? "").normalize("NFC").toLowerCase().split(/\s+/).filter(Boolean);
    const text = LocationSearchLibrary.text(item);
    if (!tokens.every((token) => text.includes(token))) return false;
    const report = item.report?.report;
    const needsReport = request.shootTypes.length || request.people !== null || request.timesOfDay.length;
    if (needsReport && !report) return false;
    if (request.shootTypes.some((type) => !["Well suited", "Workable"].includes(report.suitability.find((entry) => entry.shootType === type)?.rating))) return false;
    if (request.people !== null && (report.groupSize.cannotAssess || request.people < report.groupSize.minimum || request.people > report.groupSize.maximum)) return false;
    if (request.timesOfDay.length && !report.timesOfDay.some((entry) => entry.rating === "Recommended" && request.timesOfDay.includes(entry.period))) return false;
    if (request.setting && item.setting !== request.setting) return false;
    const names = item.tags.map((tag) => tag.name.toLowerCase());
    return request.tags.every((tag) => names.includes(tag.trim().toLowerCase()));
  }
  hold(operation) {
    let release;
    const promise = new Promise((resolve) => (release = resolve));
    this.gates[operation] = { promise, release };
    return release;
  }
  async attach(page) {
    await this.locations.attach(page);
    await page.exposeFunction("loupeLocationSearch", async (operation, input) => {
      this.calls.push({ operation, ...input });
      await this.gates[operation]?.promise;
      if (this.failures[operation] > 0) {
        this.failures[operation]--;
        return { error: "request_failed" };
      }
      const custom = this.errors[operation]?.shift();
      if (custom) return custom;
      if (operation === "tags") {
        const counts = new Map();
        for (const item of this.items)
          for (const tag of item.tags) {
            const key = tag.name.toUpperCase();
            counts.set(key, { name: counts.get(key)?.name ?? tag.name, count: (counts.get(key)?.count ?? 0) + 1, normalizedName: key });
          }
        return { data: [...counts.values()].sort((a, b) => b.count - a.count || a.normalizedName.localeCompare(b.normalizedName)) };
      }
      if (input.mode !== "keyword" && input.mode !== "meaning")
        return { error: "invalid_request", errors: { mode: ["Choose Keyword or Meaning."] } };
      const matching = this.items.filter((item) => this.matches(item, input));
      const start = Number(input.cursor ?? 0);
      const end = Math.min(matching.length, start + 24);
      return {
        data: {
          items: matching.slice(start, end).map(LocationSearchLibrary.searchItem),
          nextCursor: end < matching.length ? String(end) : null,
          totalCount: matching.length,
        },
      };
    });
  }
}
