import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CritiqueBrief,
  PHOTOGRAPH_SERVICE,
  PhotographResult,
  ServiceError,
  UploadProgress,
} from 'api';
import { DecimalPipe } from '@angular/common';

@Component({
  selector: 'lp-photograph-upload',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './photograph-upload.html',
  styleUrl: './photograph-upload.css',
})
export class PhotographUpload {
  readonly saved = output<PhotographResult>();
  readonly requestCritique = signal(false);
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  readonly image = signal<File | null>(null);
  readonly title = signal('');
  readonly brief = signal<CritiqueBrief>({
    intent: null,
    genre: null,
    experience: null,
    requestedFeedback: null,
  });
  readonly saving = signal(false);
  readonly progress = signal<UploadProgress | null>(null);
  private readonly attempted = signal(false);
  private readonly acknowledged = signal(false);
  readonly dirty = computed(
    () =>
      !this.acknowledged() &&
      (!!this.image() ||
        this.attempted() ||
        this.title().trim().length > 0 ||
        Object.values(this.brief()).some((value) => !!value?.trim())),
  );
  readonly failure = signal<string | null>(null);
  readonly failed = computed(() => this.failure() !== null);
  readonly failureMessage = computed(() => {
    switch (this.failure()) {
      case 'file_unavailable':
        return 'The selected file could not be read. Your fields are still here.';
      case 'unsupported_media':
        return 'This upload was rejected. Choose a still JPEG, PNG, HEIC or WebP image.';
      case 'image_too_large':
        return 'This upload was rejected because it exceeds the upload size limit. Choose an image of 25 MB or less.';
      case 'invalid_image':
        return 'This image could not be decoded within the limits of 100 megapixels and 20,000 pixels per edge. Choose another image.';
      case 'operation_conflict':
        return 'This retry differs from an earlier upload. Restore the original file and fields, or return to My Work to start a new upload.';
      default:
        return 'The upload was not confirmed. Your fields are still here. Retry to check whether it was saved.';
    }
  });
  readonly errors = computed(() => {
    const length = (value: string | null) =>
      [...(value ?? '').replace(/\r\n?/g, '\n').trim()].length;
    return {
      title: length(this.title()) > 200,
      intent: length(this.brief().intent) > 2000,
      genre: length(this.brief().genre) > 100,
      requestedFeedback: length(this.brief().requestedFeedback) > 2000,
    };
  });
  readonly fileError = computed(() => {
    const image = this.image();
    if (!image) return null;
    if (!image.size) return 'Choose an image that is not empty.';
    if (image.size > 25000000) return 'Choose an image of 25 MB or less.';
    const type = image.type.split(';')[0].trim().toLowerCase();
    return [
      '',
      'application/octet-stream',
      'image/jpeg',
      'image/png',
      'image/webp',
      'image/heic',
      'image/heif',
    ].includes(type)
      ? null
      : 'Choose a still JPEG, PNG, HEIC or WebP image.';
  });
  readonly invalid = computed(
    () => !this.image() || !!this.fileError() || Object.values(this.errors()).some(Boolean),
  );
  private readonly operationKey = crypto.randomUUID();

  choose(files: FileList | null): void {
    this.image.set(files?.item(0) ?? null);
  }
  setTitle(value: string): void {
    this.title.set(value);
  }
  setBrief(field: keyof CritiqueBrief, value: string): void {
    this.brief.update((brief) => ({ ...brief, [field]: value || null }));
  }
  async save(): Promise<void> {
    const image = this.image();
    if (!image || this.saving() || this.invalid()) return;
    this.saving.set(true);
    this.attempted.set(true);
    this.progress.set(null);
    this.failure.set(null);
    try {
      const photo = await this.service.upload(
        {
          image,
          title: this.title(),
          brief: this.brief(),
          operationKey: this.operationKey,
        },
        (progress) => {
          if (!this.destroy.destroyed) this.progress.set(progress);
        },
      );
      if (!this.destroy.destroyed) {
        this.acknowledged.set(true);
        this.saved.emit(photo);
      }
    } catch (error) {
      if (!this.destroy.destroyed) {
        const code = error instanceof ServiceError ? error.code : 'request_failed';
        this.failure.set(code);
        if (code === 'file_unavailable') {
          this.image.set(null);
          this.fileInput().nativeElement.value = '';
        }
      }
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
