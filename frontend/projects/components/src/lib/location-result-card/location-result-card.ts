import { Component, computed, ElementRef, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface LocationResultGroupSize {
  cannotAssess: boolean;
  minimum: number | null;
  maximum: number | null;
}
export interface LocationResultRating {
  shootType: string;
  rating: string;
}

@Component({
  selector: 'lp-location-result-card',
  imports: [RouterLink],
  templateUrl: './location-result-card.html',
  styleUrl: './location-result-card.css',
})
export class LocationResultCard {
  readonly destination = input.required<string>();
  readonly name = input.required<string>();
  readonly locality = input<string | null>(null);
  readonly coverPreviewUrl = input<string | null>(null);
  readonly imageCount = input(0);
  readonly recommendedPeriods = input<string[]>([]);
  readonly groupSize = input<LocationResultGroupSize | null>(null);
  readonly ratings = input<LocationResultRating[]>([]);
  readonly failedImage = signal<string | null>(null);
  readonly place = computed(() => {
    const count = this.imageCount();
    return [this.locality(), count ? `${count} ${count === 1 ? 'image' : 'images'}` : 'No images']
      .filter((part) => part)
      .join(' · ');
  });
  readonly hasReport = computed(() => this.groupSize() !== null);
  readonly recommended = computed(() =>
    this.recommendedPeriods().length ? `Recommended · ${this.recommendedPeriods().join(', ')}` : '',
  );
  readonly group = computed(() => {
    const size = this.groupSize();
    if (!size) return '';
    if (size.cannotAssess || size.minimum === null || size.maximum === null)
      return 'Group size · Cannot assess';
    return size.minimum === size.maximum
      ? `${size.minimum} ${size.minimum === 1 ? 'person' : 'people'}`
      : `${size.minimum}–${size.maximum} people`;
  });
  private readonly link = viewChild.required<ElementRef<HTMLAnchorElement>>('link');
  focus(): void {
    this.link().nativeElement.focus();
  }
}
