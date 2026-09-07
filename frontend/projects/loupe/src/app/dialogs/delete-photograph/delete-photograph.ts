import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  output,
  signal,
  viewChild,
} from '@angular/core';
import {
  DELETION_SERVICE,
  DeletionResult,
  PHOTOGRAPH_SERVICE,
  PhotographResult,
  ServiceError,
} from 'api';

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
  readonly conflicted = signal(false);
  readonly unavailable = signal(false);
  readonly reviewing = signal(false);
  readonly reviewed = signal(false);
  readonly reviewFailed = signal(false);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly service = inject(DELETION_SERVICE);
  private readonly photographs = inject(PHOTOGRAPH_SERVICE);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly latestHeading = viewChild<ElementRef<HTMLHeadingElement>>('latestHeading');
  open(photograph: PhotographResult): void {
    this.photograph.set(photograph);
    this.failed.set(false);
    this.conflicted.set(false);
    this.unavailable.set(false);
    this.reviewed.set(false);
    this.reviewFailed.set(false);
    this.dialog().nativeElement.showModal();
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy() && !this.reviewing()) this.dialog().nativeElement.close();
  }
  async confirm(): Promise<void> {
    const photograph = this.photograph();
    if (!photograph || this.busy() || this.reviewing() || this.conflicted() || this.unavailable())
      return;
    this.busy.set(true);
    this.failed.set(false);
    this.dialog().nativeElement.focus();
    try {
      const result = await this.service.deletePhotograph(photograph.id, photograph.revision);
      if (!this.destroyRef.destroyed) {
        this.dialog().nativeElement.close();
        this.deleted.emit(result);
      }
    } catch (error) {
      if (!this.destroyRef.destroyed) {
        if (error instanceof ServiceError && error.code === 'revision_conflict') {
          this.conflicted.set(true);
          this.reviewed.set(false);
        } else if (error instanceof ServiceError && error.code === 'item_unavailable')
          this.unavailable.set(true);
        else this.failed.set(true);
      }
    } finally {
      if (!this.destroyRef.destroyed) this.busy.set(false);
    }
  }
  async review(): Promise<void> {
    const photograph = this.photograph();
    if (!photograph || !this.conflicted() || this.reviewing()) return;
    this.reviewing.set(true);
    this.reviewFailed.set(false);
    this.dialog().nativeElement.focus();
    try {
      const latest = await this.photographs.get(photograph.id);
      if (this.destroyRef.destroyed) return;
      this.photograph.set(latest);
      this.conflicted.set(false);
      this.reviewed.set(true);
      afterNextRender(() => this.latestHeading()?.nativeElement.focus(), {
        injector: this.injector,
      });
    } catch (error) {
      if (!this.destroyRef.destroyed) {
        if (error instanceof ServiceError && error.code === 'item_unavailable')
          this.unavailable.set(true);
        else this.reviewFailed.set(true);
      }
    } finally {
      if (!this.destroyRef.destroyed) this.reviewing.set(false);
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
