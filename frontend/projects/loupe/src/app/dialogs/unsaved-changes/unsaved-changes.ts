import {
  Component,
  DestroyRef,
  DOCUMENT,
  ElementRef,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { SESSION_SERVICE } from 'api';

@Component({
  selector: 'lp-unsaved-changes',
  templateUrl: './unsaved-changes.html',
  styleUrl: './unsaved-changes.css',
})
export class UnsavedChanges {
  readonly dirty = input.required<() => boolean>();
  readonly warning = input<string | null>(null);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('discardDialog');
  private readonly session = inject(SESSION_SERVICE);
  private readonly document = inject(DOCUMENT);
  private pendingChoice: Promise<boolean> | null = null;
  private resolveChoice: ((discard: boolean) => void) | null = null;

  constructor() {
    const window = this.document.defaultView;
    const protect = (event: BeforeUnloadEvent) => {
      if (this.session.current() && this.dirty()()) {
        event.preventDefault();
        event.returnValue = '';
      }
    };
    window?.addEventListener('beforeunload', protect);
    inject(DestroyRef).onDestroy(() => {
      window?.removeEventListener('beforeunload', protect);
      this.resolveChoice?.(false);
    });
  }
  canLeave(dirty: boolean): boolean | Promise<boolean> {
    return this.session.current() && dirty ? this.confirmDiscard() : true;
  }

  confirmDiscard(): Promise<boolean> {
    if (this.pendingChoice) return this.pendingChoice;
    this.pendingChoice = new Promise((resolve) => {
      this.resolveChoice = resolve;
    });
    this.dialog().nativeElement.showModal();
    return this.pendingChoice;
  }
  finishDiscard(discard: boolean): void {
    this.dialog().nativeElement.close();
    this.resolveChoice?.(discard);
    this.resolveChoice = null;
    this.pendingChoice = null;
  }
  cancelDiscard(event: Event): void {
    event.preventDefault();
    this.finishDiscard(false);
  }
  trapDiscardFocus(event: KeyboardEvent): void {
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
