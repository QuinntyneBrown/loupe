import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DELETION_SERVICE, PHOTOGRAPHER_SERVICE, PhotographerResult, ServiceError } from 'api';
@Component({
  selector: 'lp-delete-photographer',
  templateUrl: './delete-photographer.html',
  styleUrl: './delete-photographer.css',
})
export class DeletePhotographer {
  readonly photographer = input.required<PhotographerResult>();
  readonly referenceCount = input.required<number>();
  readonly closed = output<void>();
  readonly deleted = output<void>();
  readonly reviewed = output<PhotographerResult>();
  readonly latest = signal<PhotographerResult | null>(null);
  readonly latestCount = signal<number | null>(null);
  readonly item = computed(() => this.latest() ?? this.photographer());
  readonly count = computed(() => this.latestCount() ?? this.referenceCount());
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  private readonly deletions = inject(DELETION_SERVICE);
  private readonly service = inject(PHOTOGRAPHER_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private readonly cancelButton = viewChild.required<ElementRef<HTMLButtonElement>>('cancelButton');
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy()) this.closed.emit();
  }
  async review(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const [item, references] = await Promise.all([
        this.service.get(this.photographer().id),
        this.service.references(this.photographer().id),
      ]);
      if (this.destroy.destroyed) return;
      this.latest.set(item);
      this.latestCount.set(references.totalCount);
      this.reviewed.emit(item);
      this.stale.set(false);
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.unavailable()
          ? 'This bookmark is no longer available.'
          : "Couldn't load the latest photographer. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) {
        this.busy.set(false);
        afterNextRender(
          () => {
            if (!this.destroy.destroyed) this.cancelButton().nativeElement.focus();
          },
          { injector: this.injector },
        );
      }
    }
  }
  async confirm(): Promise<void> {
    if (this.busy() || this.stale() || this.unavailable()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.deletions.deletePhotographer(this.item().id, this.item().revision);
      if (!this.destroy.destroyed) this.deleted.emit();
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.stale.set(code === 'revision_conflict');
      this.unavailable.set(code === 'item_unavailable');
      this.error.set(
        this.stale()
          ? 'This bookmark changed. Review the latest photographer before deleting.'
          : this.unavailable()
            ? 'This bookmark is no longer available.'
            : "Couldn't delete this photographer. Try Delete again; the same request is safe to retry.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
