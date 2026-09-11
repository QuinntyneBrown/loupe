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
import { DELETION_SERVICE, REFERENCE_SERVICE, ReferenceResult, ServiceError } from 'api';

@Component({
  selector: 'lp-delete-reference',
  templateUrl: './delete-reference.html',
  styleUrl: './delete-reference.css',
})
export class DeleteReference {
  readonly reference = input.required<ReferenceResult>();
  readonly closed = output<void>();
  readonly deleted = output<void>();
  readonly reviewed = output<ReferenceResult>();
  readonly latest = signal<ReferenceResult | null>(null);
  readonly busy = signal(false);
  readonly reviewing = signal(false);
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  private readonly service = inject(DELETION_SERVICE);
  private readonly references = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy() && !this.reviewing()) {
      this.modal().nativeElement.close();
      this.closed.emit();
    }
  }
  async review(): Promise<void> {
    if (this.reviewing()) return;
    this.reviewing.set(true);
    try {
      const latest = await this.references.get(this.reference().id);
      if (!this.destroy.destroyed) {
        this.latest.set(latest);
        this.reviewed.emit(latest);
        this.stale.set(false);
        this.error.set('');
      }
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.unavailable()
            ? 'This reference is no longer available.'
            : 'The latest reference could not be loaded. Try again.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.reviewing.set(false);
    }
  }
  async confirm(): Promise<void> {
    if (this.busy() || this.reviewing() || this.stale() || this.unavailable()) return;
    const item = this.latest() ?? this.reference();
    this.busy.set(true);
    this.error.set('');
    try {
      await this.service.deleteReference(item.id, item.revision);
      if (!this.destroy.destroyed) {
        this.modal().nativeElement.close();
        this.deleted.emit();
      }
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.stale.set(error instanceof ServiceError && error.code === 'revision_conflict');
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.stale()
            ? 'This reference changed. Review its latest saved details before deleting.'
            : this.unavailable()
              ? 'This reference is no longer available.'
              : 'Deletion was not confirmed. Try again to check its status.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
