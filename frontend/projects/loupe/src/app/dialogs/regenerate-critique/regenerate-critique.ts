import { Component, computed, ElementRef, output, signal, viewChild } from '@angular/core';
import { OperationResult, PhotographResult } from 'api';
import { RequestCritique } from 'domain';

@Component({
  selector: 'lp-regenerate-critique',
  imports: [RequestCritique],
  templateUrl: './regenerate-critique.html',
  styleUrl: './regenerate-critique.css',
})
export class RegenerateCritique {
  readonly photograph = signal<PhotographResult | null>(null);
  readonly admitted = output<OperationResult>();
  readonly reviewed = output<PhotographResult>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly request = viewChild(RequestCritique);
  readonly busy = computed(() => !!(this.request()?.busy() || this.request()?.reviewing()));

  open(photo: PhotographResult): void {
    if (this.dialog().nativeElement.open) return;
    this.photograph.set(photo);
    this.dialog().nativeElement.showModal();
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (this.busy()) return;
    this.dialog().nativeElement.close();
    this.photograph.set(null);
  }
  accept(operation: OperationResult): void {
    this.dialog().nativeElement.close();
    this.photograph.set(null);
    this.admitted.emit(operation);
  }
  review(photo: PhotographResult): void {
    this.photograph.set(photo);
    this.reviewed.emit(photo);
  }
  focus(): void {
    this.dialog().nativeElement.focus();
  }
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const buttons = Array.from(
      this.dialog().nativeElement.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'),
    );
    const first = buttons[0];
    const last = buttons.at(-1);
    if (!first || !last) {
      event.preventDefault();
      return;
    }
    const active = this.dialog().nativeElement.ownerDocument.activeElement;
    if (event.shiftKey && (active === first || active === this.dialog().nativeElement)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || active === this.dialog().nativeElement)) {
      event.preventDefault();
      first.focus();
    }
  }
}
