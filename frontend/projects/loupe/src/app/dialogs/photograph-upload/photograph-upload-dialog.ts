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
import { RouterLink } from '@angular/router';
import { PhotographUpload, RequestCritique } from 'domain';
import { OperationResult, PhotographResult } from 'api';
import { UnsavedChanges } from '../unsaved-changes/unsaved-changes';
import { UploadCompletion } from './upload-completion';

@Component({
  selector: 'lp-photograph-upload-dialog',
  imports: [RouterLink, PhotographUpload, UnsavedChanges, RequestCritique],
  templateUrl: './photograph-upload-dialog.html',
  styleUrl: './photograph-upload-dialog.css',
})
export class PhotographUploadDialog {
  readonly closed = output<void>();
  readonly completed = output<UploadCompletion>();
  readonly canceledTransfer = output<void>();
  private readonly injector = inject(Injector);
  private readonly progressHeading = viewChild.required<ElementRef<HTMLElement>>('progressHeading');
  focusProgress(): void {
    afterNextRender(() => this.progressHeading().nativeElement.focus(), {
      injector: this.injector,
    });
  }
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private readonly destroy = inject(DestroyRef);
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  async cancel(event?: Event): Promise<void> {
    event?.preventDefault();
    if (await this.canLeave()) {
      if (this.destroy.destroyed) return;
      const photo = this.savedPhotograph();
      if (photo) {
        this.finish(photo, 'unconfirmed');
        return;
      }
      this.modal().nativeElement.close();
      this.closed.emit();
    }
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
        'button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], summary, [tabindex="0"]',
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
  readonly form = viewChild(PhotographUpload);
  readonly savedPhotograph = signal<PhotographResult | null>(null);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly dialog = viewChild.required(UnsavedChanges);
  readonly dirty = () => this.form()?.dirty() ?? false;
  async canLeave(): Promise<boolean> {
    const leave = await this.dialog().canLeave(this.form()?.dirty() ?? false);
    if (leave && !this.destroy.destroyed && this.form()?.saving()) {
      this.form()?.cancelUpload();
      this.canceledTransfer.emit();
    }
    return leave;
  }
  saved(photo: PhotographResult): void {
    this.dialog().finishDiscard(false);
    if (this.form()?.requestCritique()) {
      this.savedPhotograph.set(photo);
      return;
    }
    this.finish(photo, 'not-requested');
  }
  admitted(operation: OperationResult): void {
    const photo = this.savedPhotograph();
    if (photo && operation.resourceId === photo.id) this.finish(photo, 'requested');
  }
  private finish(photograph: PhotographResult, critique: UploadCompletion['critique']): void {
    this.modal().nativeElement.close();
    this.completed.emit({ photograph, critique });
  }
  focusSaved(): void {
    this.heading()?.nativeElement.focus();
  }
}
