import { Component, DestroyRef, inject, output, signal } from '@angular/core';
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
    if (!image || this.saving()) return;
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
