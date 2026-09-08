import {
  referenceMetadataErrors,
  referenceMetadataFields,
} from '../reference-metadata/reference-metadata-fields';
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
import { DecimalPipe } from '@angular/common';
import { REFERENCE_SERVICE, ReferenceResult, ServiceError, UploadProgress } from 'api';

@Component({
  selector: 'lp-reference-image-panel',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './reference-image-panel.html',
  styleUrl: './reference-image-panel.css',
})
export class ReferenceImagePanel {
  readonly saved = output<ReferenceResult>();
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  readonly fields = referenceMetadataFields;
  readonly metadata = signal({ title: '', sourceUrl: '', attribution: '', notes: '' });
  readonly image = signal<File | null>(null);
  readonly saving = signal(false);
  readonly progress = signal<UploadProgress | null>(null);
  readonly failure = signal<string | null>(null);
  private readonly attempted = signal(false);
  private readonly acknowledged = signal(false);
  readonly dirty = computed(
    () =>
      !this.acknowledged() &&
      (!!this.image() ||
        this.attempted() ||
        Object.values(this.metadata()).some((value) => !!value.trim())),
  );
  private readonly operationKey = crypto.randomUUID();
  readonly errors = computed(() => referenceMetadataErrors(this.metadata(), false));
  readonly fileError = computed(() => {
    const file = this.image();
    if (!file) return null;
    if (!file.size) return 'Choose an image that is not empty.';
    if (file.size > 25000000) return 'Choose an image of 25 MB or less.';
    return [
      '',
      'application/octet-stream',
      'image/jpeg',
      'image/png',
      'image/webp',
      'image/heic',
      'image/heif',
    ].includes(file.type.split(';')[0].trim().toLowerCase())
      ? null
      : 'Choose a still JPEG, PNG, HEIC or WebP image.';
  });
  readonly invalid = computed(
    () => !this.image() || !!this.fileError() || Object.keys(this.errors()).length > 0,
  );
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
      case 'invalid_request':
        return 'The reference was not saved. Check the image and metadata, then retry.';
      case 'operation_conflict':
        return 'This retry differs from an earlier upload. Restore the original file and fields, or return to Inspiration to start a new upload.';
      default:
        return 'The upload was not confirmed. Your fields are still here. Retry to check whether it was saved.';
    }
  });
  choose(files: FileList | null): void {
    this.image.set(files?.item(0) ?? null);
  }
  setField(field: keyof ReturnType<typeof this.metadata>, value: string): void {
    this.metadata.update((metadata) => ({ ...metadata, [field]: value }));
  }
  async save(): Promise<void> {
    const image = this.image();
    if (!image || this.saving() || this.invalid()) return;
    this.saving.set(true);
    this.attempted.set(true);
    this.progress.set(null);
    this.failure.set(null);
    try {
      const reference = await this.service.upload(
        { image, ...this.metadata(), operationKey: this.operationKey },
        (progress) => {
          if (!this.destroy.destroyed) this.progress.set(progress);
        },
      );
      if (!this.destroy.destroyed) {
        this.acknowledged.set(true);
        this.saved.emit(reference);
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
