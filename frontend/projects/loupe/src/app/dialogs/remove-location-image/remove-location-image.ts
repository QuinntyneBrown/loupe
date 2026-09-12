import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { LOCATION_SERVICE, LocationImage, LocationResult, ServiceError } from 'api';

@Component({
  selector: 'lp-remove-location-image',
  templateUrl: './remove-location-image.html',
  styleUrl: './remove-location-image.css',
})
export class RemoveLocationImage {
  readonly location = input.required<LocationResult>();
  readonly image = input.required<LocationImage>();
  readonly closed = output<void>();
  readonly removed = output<LocationResult>();
  readonly reviewed = output<LocationResult>();
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (this.busy()) return;
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  async reload(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const latest = await this.service.get(this.location().id);
      if (this.destroy.destroyed) return;
      this.reviewed.emit(latest);
      this.stale.set(false);
      if (!latest.images.some((image) => image.id === this.image().id)) {
        this.unavailable.set(true);
        this.error.set('This image is no longer part of the location.');
      }
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.unavailable()
          ? 'This location is no longer available.'
          : "Couldn't load the latest details. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async confirm(): Promise<void> {
    if (this.busy() || this.stale() || this.unavailable()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.service.removeImage(
        this.location().id,
        this.image().id,
        this.location().revision,
      );
      if (this.destroy.destroyed) return;
      this.modal().nativeElement.close();
      this.removed.emit(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.stale.set(code === 'revision_conflict');
      this.unavailable.set(code === 'item_unavailable');
      this.error.set(
        this.stale()
          ? 'This location changed. Reload its latest details before removing the image.'
          : this.unavailable()
            ? 'This image is no longer available.'
            : 'Removal was not confirmed. Try again.',
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
