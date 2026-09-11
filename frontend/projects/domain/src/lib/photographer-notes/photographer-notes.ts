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
import { PHOTOGRAPHER_SERVICE, PhotographerResult, ServiceError } from 'api';
@Component({
  selector: 'lp-photographer-notes',
  imports: [FormsModule],
  templateUrl: './photographer-notes.html',
  styleUrl: './photographer-notes.css',
})
export class PhotographerNotes {
  readonly photographer = input.required<PhotographerResult>();
  readonly saved = output<PhotographerResult>();
  readonly draft = signal('');
  readonly baseline = signal<PhotographerResult | null>(null);
  readonly latest = signal<PhotographerResult | null>(null);
  readonly normalized = computed(() => this.draft().replace(/\r\n?/g, '\n').trim());
  readonly dirty = computed(
    () => !!this.baseline() && this.normalized() !== (this.baseline()?.notes ?? ''),
  );
  readonly invalid = computed(() => Array.from(this.normalized()).length > 10000);
  readonly busy = signal(false);
  readonly reviewing = signal(false);
  readonly conflict = signal(false);
  readonly unavailable = signal(false);
  readonly failed = signal(false);
  readonly error = signal('');
  readonly status = computed(() =>
    this.busy() ? 'Saving' : this.failed() ? "Couldn't save" : this.dirty() ? 'Unsaved' : 'Saved',
  );
  private readonly service = inject(PHOTOGRAPHER_SERVICE);
  private readonly destroy = inject(DestroyRef);
  constructor() {
    effect(() => {
      const item = this.photographer();
      untracked(() => {
        if (item.id !== this.baseline()?.id || !this.dirty()) {
          this.baseline.set(item);
          this.draft.set(item.notes ?? '');
        }
      });
    });
  }
  async review(): Promise<void> {
    if (this.reviewing() || this.busy()) return;
    this.reviewing.set(true);
    try {
      const latest = await this.service.get(this.photographer().id);
      if (this.destroy.destroyed) return;
      this.latest.set(latest);
      this.baseline.set(latest);
      this.conflict.set(false);
      this.failed.set(false);
      this.error.set('');
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.unavailable()
            ? 'This bookmark is no longer available. Your text is still here.'
            : "Couldn't load the latest notes. Your text is still here.",
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.reviewing.set(false);
    }
  }
  async save(): Promise<void> {
    const base = this.baseline();
    if (
      !base ||
      !this.dirty() ||
      this.busy() ||
      this.reviewing() ||
      this.conflict() ||
      this.unavailable() ||
      this.invalid()
    )
      return;
    this.busy.set(true);
    this.failed.set(false);
    this.error.set('');
    try {
      const result = await this.service.update(base.id, {
        revision: base.revision,
        name: base.name,
        portfolioUrl: base.portfolioUrl,
        summary: base.summary,
        notes: this.normalized() || null,
        tags: base.tags.map(({ name, category }) => ({ name, category })),
      });
      if (this.destroy.destroyed) return;
      this.baseline.set(result);
      this.draft.set(result.notes ?? '');
      this.latest.set(null);
      this.saved.emit(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.failed.set(true);
      this.conflict.set(error instanceof ServiceError && error.code === 'revision_conflict');
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.conflict()
          ? 'This bookmark changed. Review the latest value before saving.'
          : this.unavailable()
            ? 'This bookmark is no longer available. Your text is still here.'
            : "Couldn't save your notes. Your text is still here. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
