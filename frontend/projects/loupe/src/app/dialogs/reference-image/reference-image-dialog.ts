import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { REFERENCE_SERVICE, ReferenceResult, ServiceError, UploadProgress } from 'api';

@Component({
  selector: 'lp-reference-image-dialog',
  templateUrl: './reference-image-dialog.html',
  styleUrl: './reference-image-dialog.css',
})
export class ReferenceImageDialog {
  readonly reference = input.required<ReferenceResult>();
  readonly saved = output<ReferenceResult>();
  readonly reviewed = output<ReferenceResult>();
  readonly closed = output<void>();
  readonly file = signal<File | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly latest = signal<ReferenceResult | null>(null);
  readonly progress = signal<UploadProgress | null>(null);
  readonly dirty = computed(() => !!this.file());
  private key = crypto.randomUUID();
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  choose(files: FileList | null): void {
    if (this.busy()) return;
    this.file.set(null);
    this.error.set('');
    this.key = crypto.randomUUID();
    if (!files?.length) return;
    const file = files[0];
    if (files.length !== 1) {
      this.error.set('Choose exactly one image.');
      return;
    }
    if (
      !['image/jpeg', 'image/png', 'image/heic', 'image/heif', 'image/webp'].includes(
        file.type.toLowerCase(),
      )
    ) {
      this.error.set('Choose a JPEG, PNG, HEIC or WebP image.');
      return;
    }
    if (!file.size || file.size > 25_000_000) {
      this.error.set('Choose a nonempty image up to 25 MB.');
      return;
    }
    this.file.set(file);
  }
  drop(event: DragEvent): void {
    event.preventDefault();
    this.choose(event.dataTransfer?.files ?? null);
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy()) {
      this.modal().nativeElement.close();
      this.closed.emit();
    }
  }
  async reload(): Promise<void> {
    this.busy.set(true);
    try {
      const item = await this.service.get(this.reference().id);
      if (this.destroy.destroyed) return;
      this.latest.set(item);
      this.reviewed.emit(item);
      this.stale.set(false);
      this.error.set('');
      this.key = crypto.randomUUID();
    } catch {
      if (!this.destroy.destroyed)
        this.error.set('The latest reference could not be loaded. Your chosen file is still here.');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async save(): Promise<void> {
    const image = this.file(),
      item = this.latest() ?? this.reference();
    if (!image || this.busy() || this.stale() || this.unavailable()) return;
    this.busy.set(true);
    this.error.set('');
    this.progress.set(null);
    try {
      const saved = await this.service.replaceImage(
        item.id,
        item.revision,
        image,
        this.key,
        (progress) => {
          if (!this.destroy.destroyed) this.progress.set(progress);
        },
      );
      if (!this.destroy.destroyed) {
        this.modal().nativeElement.close();
        this.saved.emit(saved);
      }
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.stale.set(code === 'revision_conflict');
      this.unavailable.set(code === 'item_unavailable');
      if (
        ['unsupported_media', 'invalid_image', 'image_too_large', 'file_unavailable'].includes(code)
      )
        this.file.set(null);
      this.error.set(
        code === 'revision_conflict'
          ? 'This reference changed. Review the latest reference before replacing its image.'
          : code === 'item_unavailable'
            ? 'This reference is no longer available. Your chosen file is still here.'
            : !this.file()
              ? 'This image could not be read. Choose another JPEG, PNG, HEIC or WebP image up to 25 MB.'
              : 'The image save was not confirmed. Your chosen file is still here. Try again.',
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
