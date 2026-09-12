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
import { LOCATION_SERVICE, LocationResult, ServiceError } from 'api';
import { LocationForm } from 'domain';
import { UnsavedChanges } from '../unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-edit-location',
  imports: [LocationForm, UnsavedChanges],
  templateUrl: './edit-location.html',
  styleUrl: './edit-location.css',
})
export class EditLocation {
  readonly location = input.required<LocationResult>();
  readonly saved = output<LocationResult>();
  readonly closed = output<void>();
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private readonly form = viewChild.required(LocationForm);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  readonly base = signal<LocationResult | null>(null);
  readonly saving = signal(false);
  readonly reloading = signal(false);
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly retryable = signal(false);
  readonly error = signal('');
  readonly dirty = () => this.saving() || (this.form()?.dirty() ?? false);
  constructor() {
    afterNextRender(() => {
      this.base.set(this.location());
      this.modal().nativeElement.showModal();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  async cancel(event?: Event): Promise<void> {
    event?.preventDefault();
    if (this.saving() || this.reloading()) return;
    if (await this.canLeave()) {
      if (this.destroy.destroyed) return;
      this.modal().nativeElement.close();
      this.closed.emit();
    }
  }
  canLeave(): boolean | Promise<boolean> {
    return this.unsaved().canLeave(this.dirty());
  }
  backdrop(event: MouseEvent): void {
    if (event.target !== this.modal().nativeElement) return;
    const bounds = this.modal().nativeElement.getBoundingClientRect();
    if (
      event.clientX < bounds.left ||
      event.clientX > bounds.right ||
      event.clientY < bounds.top ||
      event.clientY > bounds.bottom
    )
      void this.cancel();
  }
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const controls = Array.from(
      this.modal().nativeElement.querySelectorAll<HTMLElement>(
        'button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], [tabindex="0"]',
      ),
    ).filter((control) => control.checkVisibility());
    const first = controls[0],
      last = controls.at(-1);
    if (event.shiftKey && event.target === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && event.target === last) {
      event.preventDefault();
      first?.focus();
    }
  }
  // Reload the latest saved revision while keeping the attempted values in the fields.
  async reload(): Promise<void> {
    const base = this.base();
    if (!base || this.reloading() || this.saving()) return;
    this.reloading.set(true);
    this.error.set('');
    try {
      const latest = await this.service.get(base.id);
      if (this.destroy.destroyed) return;
      this.base.set(latest);
      this.stale.set(false);
      this.retryable.set(false);
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.unavailable()
          ? 'This location is no longer available.'
          : "Couldn't load the latest details. Your edits are kept.",
      );
    } finally {
      if (!this.destroy.destroyed) this.reloading.set(false);
    }
  }
  async save(): Promise<void> {
    const base = this.base();
    if (!base || this.saving() || this.reloading() || this.stale() || this.unavailable()) return;
    this.error.set('');
    this.retryable.set(false);
    if (!this.form().validate()) return;
    this.saving.set(true);
    try {
      const { scoutingBrief: _brief, notes: _notes, tags: _tags, ...details } = this.form().value();
      const result = await this.service.update(base.id, { ...details, revision: base.revision });
      if (this.destroy.destroyed) return;
      this.unsaved().finishDiscard(false);
      this.modal().nativeElement.close();
      this.saved.emit(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const failure = error instanceof ServiceError ? error : new ServiceError('request_failed');
      this.saving.set(false);
      if (failure.code === 'invalid_request' && this.form().applyErrors(failure.errors)) return;
      this.stale.set(failure.code === 'revision_conflict');
      this.unavailable.set(failure.code === 'item_unavailable');
      this.retryable.set(
        !['revision_conflict', 'invalid_request', 'item_unavailable'].includes(failure.code),
      );
      this.error.set(
        this.stale()
          ? 'This location changed since you opened it. Reload latest, then save your edits again.'
          : this.unavailable()
            ? 'This location is no longer available.'
            : failure.code === 'invalid_request'
              ? "Couldn't save these changes. Check the details; your edits are kept."
              : "Couldn't save these changes. Try again; your edits are kept.",
      );
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
