import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { LOCATION_SERVICE, LocationResult, ServiceError } from 'api';
import { LocationForm } from 'domain';
import { UnsavedChanges } from '../unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-add-location',
  imports: [LocationForm, UnsavedChanges],
  templateUrl: './add-location.html',
  styleUrl: './add-location.css',
})
export class AddLocation {
  readonly saved = output<LocationResult>();
  readonly closed = output<void>();
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private readonly form = viewChild.required(LocationForm);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly retryable = signal(false);
  private operationKey = crypto.randomUUID();
  readonly dirty = () => this.saving() || (this.form()?.dirty() ?? false);
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  async cancel(event?: Event): Promise<void> {
    event?.preventDefault();
    if (this.saving()) return;
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
  async save(): Promise<void> {
    if (this.saving()) return;
    this.error.set('');
    this.retryable.set(false);
    if (!this.form().validate()) return;
    this.saving.set(true);
    try {
      const result = await this.service.create(this.form().value(), this.operationKey);
      if (this.destroy.destroyed) return;
      this.unsaved().finishDiscard(false);
      this.saved.emit(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const failure = error instanceof ServiceError ? error : new ServiceError('request_failed');
      // Re-enable the fields before placing focus on the first invalid one.
      this.saving.set(false);
      if (failure.code === 'invalid_request' && this.form().applyErrors(failure.errors)) return;
      if (failure.code === 'invalid_request') this.operationKey = crypto.randomUUID();
      this.retryable.set(failure.code !== 'invalid_request');
      this.error.set(
        failure.code === 'invalid_request'
          ? "Couldn't save this location. Check the details; everything you typed is kept."
          : "Couldn't save this location. Try again; everything you typed is kept.",
      );
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
