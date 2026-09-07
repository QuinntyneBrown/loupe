import {
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PHOTOGRAPH_SERVICE, PhotographResult, ServiceError } from 'api';

@Component({
  selector: 'lp-photograph-notes',
  imports: [FormsModule],
  templateUrl: './photograph-notes.html',
  styleUrl: './photograph-notes.css',
})
export class PhotographNotes {
  readonly photograph = input.required<PhotographResult>();
  readonly saved = output<PhotographResult>();
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  readonly draft = signal('');
  private readonly savedText = signal('');
  private readonly revision = signal(1);
  readonly saving = signal(false);
  readonly failed = signal(false);
  readonly conflicted = signal(false);
  readonly reloading = signal(false);
  readonly reloadFailed = signal(false);
  readonly latestNotes = signal<string | null>(null);
  readonly normalized = computed(() => this.draft().replace(/\r\n?/g, '\n').trim());
  readonly length = computed(() => [...this.normalized()].length);
  readonly dirty = computed(() => this.normalized() !== this.savedText());
  readonly overLimit = computed(() => this.length() > 10000);
  private initializedId = '';

  constructor() {
    effect(() => {
      const photo = this.photograph();
      untracked(() => {
        if (photo.id !== this.initializedId || !this.dirty()) {
          this.initializedId = photo.id;
          this.draft.set(photo.notes ?? '');
          this.savedText.set(photo.notes ?? '');
          this.revision.set(photo.revision);
        }
      });
    });
  }
  edit(value: string): void {
    this.draft.set(value);
    this.failed.set(false);
  }
  async save(): Promise<void> {
    if (this.saving() || this.reloading() || this.conflicted() || !this.dirty() || this.overLimit())
      return;
    this.saving.set(true);
    this.failed.set(false);
    try {
      const photo = await this.service.updateNotes(
        this.photograph().id,
        this.revision(),
        this.draft(),
      );
      if (this.destroy.destroyed) return;
      this.draft.set(photo.notes ?? '');
      this.savedText.set(photo.notes ?? '');
      this.revision.set(photo.revision);
      this.latestNotes.set(null);
      this.saved.emit(photo);
    } catch (error) {
      if (!this.destroy.destroyed) {
        if (error instanceof ServiceError && error.code === 'revision_conflict') {
          this.conflicted.set(true);
          this.latestNotes.set(null);
        } else this.failed.set(true);
      }
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
  async reloadLatest(): Promise<void> {
    if (!this.conflicted() || this.reloading()) return;
    this.reloading.set(true);
    this.reloadFailed.set(false);
    try {
      const photo = await this.service.get(this.photograph().id);
      if (this.destroy.destroyed) return;
      this.revision.set(photo.revision);
      this.savedText.set(photo.notes ?? '');
      this.latestNotes.set(photo.notes ?? '');
      this.conflicted.set(false);
      this.saved.emit(photo);
    } catch {
      if (!this.destroy.destroyed) this.reloadFailed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.reloading.set(false);
    }
  }
}
