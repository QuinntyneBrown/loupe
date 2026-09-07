import {
  Component,
  DestroyRef,
  DOCUMENT,
  effect,
  ElementRef,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { PhotographDetail } from 'domain';
import { SESSION_SERVICE } from 'api';

@Component({
  selector: 'lp-photograph-detail-page',
  imports: [RouterLink, PhotographDetail],
  templateUrl: './photograph-detail-page.html',
  styleUrl: './photograph-detail-page.css',
})
export class PhotographDetailPage {
  readonly id = input.required<string>();
  private readonly detail = viewChild(PhotographDetail);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('discardDialog');
  private readonly keepButton = viewChild.required<ElementRef<HTMLButtonElement>>('keepButton');
  private readonly discardButton =
    viewChild.required<ElementRef<HTMLButtonElement>>('discardButton');
  private readonly session = inject(SESSION_SERVICE);
  private readonly document = inject(DOCUMENT);
  private pendingChoice: Promise<boolean> | null = null;
  private resolveChoice: ((discard: boolean) => void) | null = null;

  constructor() {
    effect((onCleanup) => {
      const window = this.document.defaultView;
      if (!window || !this.session.current() || !this.detail()?.dirty()) return;
      const protect = (event: BeforeUnloadEvent) => {
        event.preventDefault();
        event.returnValue = '';
      };
      window.addEventListener('beforeunload', protect);
      onCleanup(() => window.removeEventListener('beforeunload', protect));
    });
    inject(DestroyRef).onDestroy(() => this.resolveChoice?.(false));
  }
  canLeave(): boolean | Promise<boolean> {
    return this.session.current() && this.detail()?.dirty() ? this.confirmDiscard() : true;
  }
  async requestDiscard(discard: () => void): Promise<void> {
    if (await this.confirmDiscard()) discard();
  }
  private confirmDiscard(): Promise<boolean> {
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
    const first = this.keepButton().nativeElement;
    const last = this.discardButton().nativeElement;
    if (event.shiftKey ? event.target === first : event.target === last) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  }
}
