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
import { ServiceError, VIDEO_SERVICE, VideoResult } from 'api';

@Component({
  selector: 'lp-delete-video',
  templateUrl: './delete-video.html',
  styleUrl: './delete-video.css',
})
export class DeleteVideo {
  readonly video = input.required<VideoResult>();
  readonly closed = output<void>();
  readonly deleted = output<VideoResult>();
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  private revision: number | null = null;
  private readonly service = inject(VIDEO_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy()) this.closed.emit();
  }
  async review(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const latest = await this.service.get(this.video().id);
      if (this.destroy.destroyed) return;
      this.revision = latest.revision;
      this.stale.set(false);
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.unavailable()
          ? 'This video is no longer available.'
          : "Couldn't load the latest video. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async confirm(): Promise<void> {
    if (this.busy() || this.stale() || this.unavailable()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.service.delete(this.video().id, this.revision ?? this.video().revision);
      if (!this.destroy.destroyed) this.deleted.emit(this.video());
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.stale.set(code === 'revision_conflict');
      this.unavailable.set(code === 'item_unavailable');
      this.error.set(
        this.stale()
          ? 'This video changed. Review the latest video before deleting.'
          : this.unavailable()
            ? 'This video is no longer available.'
            : "Couldn't delete this video. Try Delete again; the same request is safe to retry.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
