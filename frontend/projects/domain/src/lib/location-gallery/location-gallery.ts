import { Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { LocationResult } from 'api';

@Component({
  selector: 'lp-location-gallery',
  templateUrl: './location-gallery.html',
  styleUrl: './location-gallery.css',
})
export class LocationGallery {
  readonly location = input.required<LocationResult>();
  readonly addImagesRequested = output<void>();
  readonly maximum = 10;
  readonly selectedId = signal<string | null>(null);
  readonly images = computed(() => this.location().images);
  readonly selected = computed(
    () => this.images().find((image) => image.id === this.selectedId()) ?? this.images()[0] ?? null,
  );
  readonly selectedIndex = computed(() =>
    this.images().findIndex((image) => image.id === this.selected()?.id),
  );
  readonly isCover = (id: string) => this.location().coverImageId === id;
  readonly remaining = computed(() => this.maximum - this.images().length);
  constructor() {
    effect(() => {
      const images = this.images();
      untracked(() => {
        if (!images.some((image) => image.id === this.selectedId()))
          this.selectedId.set(images[0]?.id ?? null);
      });
    });
  }
  select(id: string): void {
    this.selectedId.set(id);
  }
}
