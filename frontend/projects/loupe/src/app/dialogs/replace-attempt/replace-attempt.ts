import { Component, ElementRef, output, signal, viewChild } from '@angular/core';
import { AttemptPicker } from 'domain';

@Component({
  selector: 'lp-replace-attempt',
  imports: [AttemptPicker],
  templateUrl: './replace-attempt.html',
  styleUrl: './replace-attempt.css',
})
export class ReplaceAttempt {
  readonly context = signal<{ side: 'first' | 'second'; excludeId: string } | null>(null);
  readonly chosen = output<{ side: 'first' | 'second'; id: string }>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  open(side: 'first' | 'second', excludeId: string): void {
    if (this.dialog().nativeElement.open) return;
    this.context.set({ side, excludeId });
    this.dialog().nativeElement.showModal();
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    this.dialog().nativeElement.close();
    this.context.set(null);
  }
  accept(id: string): void {
    const context = this.context();
    if (!context) return;
    this.cancel();
    this.chosen.emit({ side: context.side, id });
  }
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const buttons = Array.from(
      this.dialog().nativeElement.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'),
    );
    const first = buttons[0],
      last = buttons.at(-1),
      active = this.dialog().nativeElement.ownerDocument.activeElement;
    if (event.shiftKey && active === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first?.focus();
    }
  }
}
