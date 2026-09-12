import {
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  ElementRef,
  viewChild,
} from '@angular/core';
import { LOCATION_SERVICE, LocationImage, LocationResult, ServiceError } from 'api';

@Component({
  selector: 'lp-location-gallery',
  templateUrl: './location-gallery.html',
  styleUrl: './location-gallery.css',
})
export class LocationGallery {
  readonly location = input.required<LocationResult>();
  readonly addImagesRequested = output<void>();
  readonly removeRequested = output<LocationImage>();
  readonly saved = output<LocationResult>();
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly root = viewChild.required<ElementRef<HTMLElement>>('root');
  focus(): void {
    const element = this.root().nativeElement;
    (
      element.querySelector<HTMLElement>('input[type=radio]:checked') ??
      element.querySelector<HTMLElement>('button')
    )?.focus();
  }
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
  async setCover(): Promise<void> {
    const image = this.selected();
    const location = this.location();
    if (!image || this.busy() || this.isCover(image.id)) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.service.setCover(location.id, image.id, location.revision);
      if (!this.destroy.destroyed) this.saved.emit(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.error.set(
        code === 'revision_conflict'
          ? 'This location changed. Reload the page before changing the cover.'
          : code === 'item_unavailable'
            ? 'This image is no longer available.'
            : "Couldn't change the cover. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
