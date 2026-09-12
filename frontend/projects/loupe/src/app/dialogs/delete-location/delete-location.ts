import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DELETION_SERVICE, LOCATION_SERVICE, LocationResult, ServiceError } from 'api';

@Component({
  selector: 'lp-delete-location',
  templateUrl: './delete-location.html',
  styleUrl: './delete-location.css',
})
export class DeleteLocation {
  readonly id = input.required<string>();
  readonly name = input.required<string>();
  readonly closed = output<void>();
  readonly deleted = output<void>();
  readonly latest = signal<LocationResult | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  readonly effects = computed(() => {
    const item = this.latest();
    const count = item?.images.length ?? 0;
    const images = count === 1 ? '1 image' : `${count} images`;
    return `Its ${images}, scouting report, notes, tags, and search record are all removed. This can't be undone.`;
  });
  private readonly deletions = inject(DELETION_SERVICE);
  private readonly locations = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  constructor() {
    afterNextRender(() => {
      this.modal().nativeElement.showModal();
      void this.load();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (this.busy()) return;
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const latest = await this.locations.get(this.id());
      if (this.destroy.destroyed) return;
      this.latest.set(latest);
      this.stale.set(false);
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.unavailable()
          ? 'This location is no longer available.'
          : "Couldn't load the latest details. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.loading.set(false);
    }
  }
  async confirm(): Promise<void> {
    const item = this.latest();
    if (!item || this.busy() || this.loading() || this.stale() || this.unavailable()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.deletions.deleteLocation(item.id, item.revision);
      if (this.destroy.destroyed) return;
      this.modal().nativeElement.close();
      this.deleted.emit();
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.stale.set(error instanceof ServiceError && error.code === 'revision_conflict');
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.stale()
          ? 'This location changed. Reload its latest details before deleting.'
          : this.unavailable()
            ? 'This location is no longer available.'
            : 'Deletion was not confirmed. Try again to check its status.',
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
