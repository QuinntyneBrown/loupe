import { Component, computed, DestroyRef, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CritiqueBrief, PHOTOGRAPH_SERVICE, PhotographResult } from 'api';

@Component({
  selector: 'lp-photograph-upload',
  imports: [FormsModule],
  templateUrl: './photograph-upload.html',
  styleUrl: './photograph-upload.css',
})
export class PhotographUpload {
  readonly saved = output<PhotographResult>();
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  readonly image = signal<File | null>(null);
  readonly title = signal('');
  readonly brief = signal<CritiqueBrief>({
    intent: null,
    genre: null,
    experience: null,
    requestedFeedback: null,
  });
  readonly saving = signal(false);
  readonly failed = signal(false);
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
  private operationKey = crypto.randomUUID();

  choose(files: FileList | null): void {
    this.image.set(files?.item(0) ?? null);
    this.operationKey = crypto.randomUUID();
    this.failed.set(false);
  }
  setTitle(value: string): void {
    this.title.set(value);
    this.operationKey = crypto.randomUUID();
  }
  setBrief(field: keyof CritiqueBrief, value: string): void {
    this.brief.update((brief) => ({ ...brief, [field]: value || null }));
    this.operationKey = crypto.randomUUID();
  }
  async save(): Promise<void> {
    const image = this.image();
    if (!image || this.saving() || this.invalid()) return;
    this.saving.set(true);
    this.failed.set(false);
    try {
      const photo = await this.service.upload({
        image,
        title: this.title(),
        brief: this.brief(),
        operationKey: this.operationKey,
      });
      if (!this.destroy.destroyed) this.saved.emit(photo);
    } catch {
      if (!this.destroy.destroyed) this.failed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
