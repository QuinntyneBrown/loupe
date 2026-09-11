import { Component, computed, effect, input, output, signal } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { CritiquePresentation, CritiqueEvidence, EvidenceRegion } from './critique-presentation';

@Component({
  selector: 'lp-critique-content',
  imports: [DatePipe, NgTemplateOutlet],
  templateUrl: './critique-content.html',
  styleUrl: './critique-content.css',
  host: { '(document:keydown.escape)': 'clearSelection()' },
})
export class CritiqueContent {
  readonly content = input.required<CritiquePresentation>();
  readonly imageUrl = input<string | null>(null);
  readonly imageWidth = input(1);
  readonly imageHeight = input(1);
  readonly regionChanged = output<EvidenceRegion | null>();
  readonly hovered = signal<CritiqueEvidence | null>(null);
  readonly focused = signal<CritiqueEvidence | null>(null);
  readonly pinned = signal<CritiqueEvidence | null>(null);
  readonly active = computed(() => this.pinned() ?? this.hovered() ?? this.focused());

  constructor() {
    effect(() => {
      this.content();
      this.imageUrl();
      this.clearSelection();
    });
    effect(() => this.regionChanged.emit(this.imageUrl() ? (this.active()?.region ?? null) : null));
  }
  clearSelection(): void {
    this.hovered.set(null);
    this.focused.set(null);
    this.pinned.set(null);
  }
  toggle(statement: CritiqueEvidence): void {
    if (this.pinned() === statement) this.clearSelection();
    else this.pinned.set(statement);
  }
  hasRegions(statements: CritiqueEvidence[]): boolean {
    return !!this.imageUrl() && statements.some((statement) => !!statement.region);
  }
  crop(region: EvidenceRegion): string {
    const diameter = region.size * this.imageWidth();
    return `${region.x * this.imageWidth() - diameter / 2} ${region.y * this.imageHeight() - diameter / 2} ${diameter} ${diameter}`;
  }
}
