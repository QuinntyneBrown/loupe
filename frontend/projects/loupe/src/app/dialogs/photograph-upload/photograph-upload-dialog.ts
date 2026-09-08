import { afterNextRender, Component, DestroyRef, ElementRef, inject, Injector, output, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PhotographUpload, RequestCritique } from 'domain';
import { OperationResult, PhotographResult } from 'api';
import { UnsavedChanges } from '../unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-photograph-upload-dialog',
  imports: [RouterLink, PhotographUpload, UnsavedChanges, RequestCritique],
  templateUrl: './photograph-upload-dialog.html',
  styleUrl: './photograph-upload-dialog.css',
})
export class PhotographUploadDialog {
  readonly closed = output<void>();
  readonly canceledTransfer = output<void>();
  private readonly injector = inject(Injector);
  private readonly progressHeading = viewChild.required<ElementRef<HTMLElement>>('progressHeading');
  focusProgress(): void {
    afterNextRender(() => this.progressHeading().nativeElement.focus(), { injector: this.injector });
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
      this.modal().nativeElement.close();
      this.closed.emit();
    }
  }
  backdrop(event: MouseEvent): void {
    if (event.target !== this.modal().nativeElement) return;
    const bounds = this.modal().nativeElement.getBoundingClientRect();
    if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)
      void this.cancel();
  }
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const controls = Array.from(this.modal().nativeElement.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], summary, [tabindex="0"]')).filter(control => control.checkVisibility());
    const first = controls[0], last = controls.at(-1);
    if (event.shiftKey && event.target === first) { event.preventDefault(); last?.focus(); }
    else if (!event.shiftKey && event.target === last) { event.preventDefault(); first?.focus(); }
  }
  readonly form = viewChild(PhotographUpload);
  readonly savedPhotograph = signal<PhotographResult | null>(null);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly dialog = viewChild.required(UnsavedChanges);
  private readonly router = inject(Router);
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
    if (this.form()?.requestCritique()) {
      this.savedPhotograph.set(photo);
      return;
    }
    void this.router.navigate(['/my-work', photo.id]);
  }
  admitted(operation: OperationResult): void {
    if (operation.resourceId === this.savedPhotograph()?.id)
      void this.router.navigate(['/my-work', operation.resourceId]);
  }
  focusSaved(): void {
    this.heading()?.nativeElement.focus();
  }
}
