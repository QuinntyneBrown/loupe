import { Component, computed, ElementRef, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

export type LocationCardStatus = 'None' | 'Queued' | 'Running' | 'Ready' | 'Outdated' | 'Failed';

@Component({
  selector: 'lp-location-card',
  imports: [RouterLink],
  templateUrl: './location-card.html',
  styleUrl: './location-card.css',
})
export class LocationCard {
  readonly destination = input.required<string>();
  readonly name = input.required<string>();
  readonly locality = input<string | null>(null);
  readonly coverPreviewUrl = input<string | null>(null);
  readonly imageCount = input(0);
  readonly reportStatus = input<LocationCardStatus>('None');
  readonly failedImage = signal<string | null>(null);
  readonly status = computed(() => {
    switch (this.reportStatus()) {
      case 'Queued':
        return { label: 'Queued', modifier: 'lp-status--queued' };
      case 'Running':
        return { label: 'Scouting', modifier: 'lp-status--analyzing' };
      case 'Ready':
        return { label: 'Report', modifier: 'lp-status--ready' };
      case 'Outdated':
        return { label: 'Outdated', modifier: 'lp-status--outdated' };
      case 'Failed':
        return { label: 'Failed', modifier: 'lp-status--failed' };
      default:
        return null;
    }
  });
  readonly meta = computed(() => {
    const count = this.imageCount();
    const parts = [
      this.locality(),
      count ? `${count} ${count === 1 ? 'image' : 'images'}` : 'No images',
      this.status() ? null : 'No scouting report',
    ];
    return parts.filter((part) => part).join(' · ');
  });
  private readonly link = viewChild.required<ElementRef<HTMLAnchorElement>>('link');
  focus(): void {
    this.link().nativeElement.focus();
  }
}
