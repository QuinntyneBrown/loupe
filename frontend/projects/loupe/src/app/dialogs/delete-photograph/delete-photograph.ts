import {
  Component,
  DestroyRef,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DELETION_SERVICE, DeletionResult, PhotographResult } from 'api';

@Component({
  selector: 'lp-delete-photograph',
  templateUrl: './delete-photograph.html',
  styleUrl: './delete-photograph.css',
})
export class DeletePhotograph {
  readonly deleted = output<DeletionResult>();
  readonly photograph = signal<PhotographResult | null>(null);
  readonly busy = signal(false);
  readonly failed = signal(false);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly service = inject(DELETION_SERVICE);
  private readonly destroyRef = inject(DestroyRef);
  open(photograph: PhotographResult): void {
    this.photograph.set(photograph);
    this.failed.set(false);
    this.dialog().nativeElement.showModal();
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy()) this.dialog().nativeElement.close();
  }
  async confirm(): Promise<void> {
    const photograph = this.photograph();
    if (!photograph || this.busy()) return;
    this.busy.set(true);
    this.failed.set(false);
    this.dialog().nativeElement.focus();
    try {
      const result = await this.service.deletePhotograph(photograph.id, photograph.revision);
      if (!this.destroyRef.destroyed) {
        this.dialog().nativeElement.close();
        this.deleted.emit(result);
      }
    } catch {
      if (!this.destroyRef.destroyed) this.failed.set(true);
    } finally {
      if (!this.destroyRef.destroyed) this.busy.set(false);
    }
  }
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const controls =
      this.dialog().nativeElement.querySelectorAll<HTMLButtonElement>('button:not([disabled])');
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (!first) {
      event.preventDefault();
      return;
    }
    if (event.shiftKey ? event.target === first : event.target === last) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  }
}
