import { Component, computed, input, output } from '@angular/core';

export interface CitedImage {
  id: string;
  index: number;
  previewUrl: string;
}

interface Row {
  label: string;
  rating: string | null;
  tone: 'success' | 'warning' | 'outline' | 'plain' | null;
  basis: 'Visible' | 'Inferred';
  text: string;
  citedImageIds: string[];
}

interface Section {
  id: string;
  heading: string;
  rows: Row[];
}

type Entry = { basis: 'Visible' | 'Inferred'; citedImageIds: string[] };

@Component({
  selector: 'lp-scouting-report-content',
  templateUrl: './scouting-report-content.html',
  styleUrl: './scouting-report-content.css',
})
export class ScoutingReportContent {
  readonly report = input.required<{
    overview: (Entry & { strength: string; reason: string })[];
    suitability: (Entry & { shootType: string; rating: string; reason: string })[];
    timesOfDay: (Entry & { period: string; rating: string; reason: string })[];
    techniques: (Entry & { technique: string; explanation: string })[];
    groupSize: Entry & {
      cannotAssess: boolean;
      minimum: number | null;
      maximum: number | null;
      reason: string;
    };
    cautions: (Entry & { caution: string })[];
  }>();
  readonly images = input<CitedImage[]>([]);
  readonly imageSelected = output<string>();
  readonly sections = computed<Section[]>(() => {
    const report = this.report();
    const group = report.groupSize;
    return [
      {
        id: 'overview',
        heading: 'Overview',
        rows: report.overview.map((entry) => ({
          label: entry.strength,
          rating: null,
          tone: null,
          basis: entry.basis,
          text: entry.reason,
          citedImageIds: entry.citedImageIds,
        })),
      },
      {
        id: 'suitability',
        heading: 'Suitability',
        rows: report.suitability.map((entry) => ({
          label: entry.shootType,
          rating: entry.rating,
          tone:
            entry.rating === 'Well suited'
              ? 'success'
              : entry.rating === 'Not recommended'
                ? 'warning'
                : entry.rating === 'Cannot assess'
                  ? 'outline'
                  : 'plain',
          basis: entry.basis,
          text: entry.reason,
          citedImageIds: entry.citedImageIds,
        })),
      },
      {
        id: 'time',
        heading: 'Time of day',
        rows: report.timesOfDay.map((entry) => ({
          label: entry.period,
          rating: entry.rating,
          tone:
            entry.rating === 'Recommended'
              ? 'success'
              : entry.rating === 'Avoid'
                ? 'warning'
                : 'outline',
          basis: entry.basis,
          text: entry.reason,
          citedImageIds: entry.citedImageIds,
        })),
      },
      {
        id: 'techniques',
        heading: 'Techniques',
        rows: report.techniques.map((entry) => ({
          label: entry.technique,
          rating: null,
          tone: null,
          basis: entry.basis,
          text: entry.explanation,
          citedImageIds: entry.citedImageIds,
        })),
      },
      {
        id: 'group',
        heading: 'Group size',
        rows: [
          {
            label: group.cannotAssess
              ? 'Cannot assess'
              : group.minimum === group.maximum
                ? `${group.minimum} ${group.minimum === 1 ? 'person' : 'people'}`
                : `${group.minimum}–${group.maximum} people`,
            rating: null,
            tone: null,
            basis: group.basis,
            text: group.reason,
            citedImageIds: group.citedImageIds,
          },
        ],
      },
      {
        id: 'cautions',
        heading: 'Cautions',
        rows: report.cautions.map((entry) => ({
          label: entry.caution,
          rating: null,
          tone: null,
          basis: entry.basis,
          text: '',
          citedImageIds: entry.citedImageIds,
        })),
      },
    ];
  });
  cited(ids: string[]): CitedImage[] {
    return ids
      .map((id) => this.images().find((image) => image.id === id))
      .filter((image): image is CitedImage => !!image);
  }
}
