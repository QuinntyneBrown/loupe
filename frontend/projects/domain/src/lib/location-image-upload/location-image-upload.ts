import {
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
import { DecimalPipe } from '@angular/common';
import { LOCATION_SERVICE, LocationResult, ServiceError, UploadProgress } from 'api';

export type UploadItemState = 'queued' | 'uploading' | 'saved' | 'failed' | 'invalid';

export interface UploadItem {
  key: string;
  file: File;
  state: UploadItemState;
  progress: UploadProgress | null;
  reason: string;
  retryable: boolean;
}

const acceptedTypes = [
  '',
  'application/octet-stream',
  'image/jpeg',
  'image/png',
  'image/webp',
  'image/heic',
  'image/heif',
];

@Component({
  selector: 'lp-location-image-upload',
  imports: [DecimalPipe],
  templateUrl: './location-image-upload.html',
  styleUrl: './location-image-upload.css',
})
export class LocationImageUpload {
  readonly location = input.required<LocationResult>();
  readonly saved = output<LocationResult>();
  readonly closeRequested = output<void>();
  readonly canceled = output<void>();
  readonly maximum = 10;
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  readonly selected = signal<File[]>([]);
  readonly items = signal<UploadItem[]>([]);
  readonly rejection = signal('');
  readonly rejectionDetail = signal('');
  private controller: AbortController | null = null;
  readonly savedCount = computed(
    () => this.items().filter((item) => item.state === 'saved').length,
  );
  readonly remaining = computed(() => this.maximum - this.location().images.length);
  readonly uploading = computed(() =>
    this.items().some((item) => item.state === 'queued' || item.state === 'uploading'),
  );
  readonly started = computed(() => this.items().length > 0);
  readonly dirty = computed(() => this.selected().length > 0 || this.uploading());
  readonly canUpload = computed(() => this.selected().length > 0 && !this.rejection());
  choose(files: FileList | null): void {
    const chosen = files ? Array.from(files) : [];
    this.rejection.set('');
    this.rejectionDetail.set('');
    if (chosen.length > this.remaining()) {
      const fit = this.remaining();
      this.rejection.set(
        `You chose ${chosen.length} ${chosen.length === 1 ? 'image' : 'images'}, but only ${fit} more ${fit === 1 ? 'fits' : 'fit'}.`,
      );
      this.rejectionDetail.set(
        `A location holds up to ${this.maximum} images and this one has ${this.location().images.length}. Nothing was uploaded; choose up to ${fit}.`,
      );
      this.selected.set([]);
      return;
    }
    this.selected.set(chosen);
  }
  start(): void {
    if (!this.canUpload() || this.uploading()) return;
    this.controller = new AbortController();
    this.items.set(
      this.selected().map((file) => ({
        key: crypto.randomUUID(),
        file,
        state: LocationImageUpload.invalidReason(file) ? 'invalid' : 'queued',
        progress: null,
        reason: LocationImageUpload.invalidReason(file) ?? '',
        retryable: false,
      })),
    );
    this.selected.set([]);
    for (const item of this.items()) if (item.state === 'queued') void this.upload(item.key);
  }
  retry(key: string): void {
    const item = this.items().find((item) => item.key === key);
    if (!item || item.state !== 'failed') return;
    this.controller ??= new AbortController();
    void this.upload(key);
  }
  remove(key: string): void {
    this.items.update((items) => items.filter((item) => item.key !== key));
  }
  cancelRemaining(): void {
    this.controller?.abort();
    this.controller = null;
    this.items.update((items) =>
      items.map((item) =>
        item.state === 'queued' || item.state === 'uploading'
          ? { ...item, state: 'failed', reason: 'Cancelled before it finished.', retryable: false }
          : item,
      ),
    );
    this.canceled.emit();
  }
  private update(key: string, change: Partial<UploadItem>): void {
    this.items.update((items) =>
      items.map((item) => (item.key === key ? { ...item, ...change } : item)),
    );
  }
  private async upload(key: string): Promise<void> {
    const item = this.items().find((item) => item.key === key);
    const signal = this.controller?.signal;
    if (!item || !signal) return;
    this.update(key, { state: 'uploading', reason: '', progress: null, retryable: false });
    try {
      const result = await this.service.addImage(
        this.location().id,
        item.file,
        item.key,
        (progress) => {
          if (!this.destroy.destroyed) this.update(key, { progress });
        },
        signal,
      );
      if (this.destroy.destroyed) return;
      this.update(key, { state: 'saved', progress: null });
      this.saved.emit(result);
    } catch (error) {
      if (this.destroy.destroyed || signal.aborted) return;
      const code = error instanceof ServiceError ? error.code : 'request_failed';
      const invalid = [
        'unsupported_media',
        'invalid_image',
        'image_too_large',
        'file_unavailable',
      ].includes(code);
      this.update(key, {
        state: invalid ? 'invalid' : 'failed',
        retryable: !invalid,
        reason:
          code === 'unsupported_media' || code === 'invalid_image'
            ? 'Use JPEG, PNG, HEIC or WebP up to 25 MB. Export a JPEG at full size and add it afterwards.'
            : code === 'image_too_large'
              ? 'Choose an image of 25 MB or less.'
              : code === 'file_unavailable'
                ? 'The file is no longer available. Choose it again.'
                : code === 'invalid_request'
                  ? 'This location has no room for another image.'
                  : code === 'item_unavailable'
                    ? 'This location is no longer available.'
                    : 'The connection dropped part way through. Nothing from this file was kept.',
      });
    }
  }
  title(item: UploadItem): string {
    return item.state === 'invalid' && !item.retryable && item.reason.startsWith('Use JPEG')
      ? `${item.file.name} is a format Loupe can't read.`
      : `${item.file.name} didn't upload.`;
  }
  static invalidReason(file: File): string | null {
    if (!file.size) return 'Choose an image that is not empty.';
    if (file.size > 25000000) return 'Choose an image of 25 MB or less.';
    const type = file.type.split(';')[0].trim().toLowerCase();
    return acceptedTypes.includes(type)
      ? null
      : 'Use JPEG, PNG, HEIC or WebP up to 25 MB. Export a JPEG at full size and add it afterwards.';
  }
  focusInput(): void {
    this.fileInput()?.nativeElement.focus();
  }
}
