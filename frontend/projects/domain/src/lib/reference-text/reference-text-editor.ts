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
import { REFERENCE_SERVICE, ReferenceResult, ServiceError } from 'api';

@Component({
  selector: 'lp-reference-text-editor',
  imports: [FormsModule],
  templateUrl: './reference-text-editor.html',
  styleUrl: './reference-text-editor.css',
})
export class ReferenceTextEditor {
  readonly reference = input.required<ReferenceResult>();
  readonly field = input.required<'description' | 'notes'>();
  readonly saved = output<ReferenceResult>();
  readonly label = computed(() => (this.field() === 'description' ? 'Description' : 'Your notes'));
  readonly draft = signal('');
  readonly baseline = signal<ReferenceResult | null>(null);
  readonly normalized = computed(() => this.draft().replace(/\r\n?/g, '\n').trim());
  readonly dirty = computed(
    () => !!this.baseline() && this.normalized() !== (this.baseline()?.[this.field()] ?? ''),
  );
  readonly busy = signal(false);
  readonly reviewing = signal(false);
  readonly failed = signal(false);
  readonly conflict = signal(false);
  readonly unavailable = signal(false);
  readonly latest = signal<ReferenceResult | null>(null);
  readonly error = signal('');
  readonly maximum = computed(() => (this.field() === 'description' ? 4000 : 10000));
  readonly invalid = computed(() => [...this.normalized()].length > this.maximum());
  readonly status = computed(() =>
    this.busy() ? 'Saving' : this.failed() ? "Couldn't save" : this.dirty() ? 'Unsaved' : 'Saved',
  );
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  constructor() {
    effect(() => {
      const reference = this.reference(),
        field = this.field();
      untracked(() => {
        if (reference.id !== this.baseline()?.id || !this.dirty()) {
          this.baseline.set(reference);
          this.draft.set(reference[field] ?? '');
        }
      });
    });
  }
  async review(): Promise<void> {
    if (this.reviewing()) return;
    this.reviewing.set(true);
    try {
      const latest = await this.service.get(this.reference().id);
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
            ? 'This reference is no longer available. Your text is still here.'
            : 'The latest value could not be loaded. Your text is still here. Try again.',
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
      const result = await this.service.updateText(
        base.id,
        base.revision,
        this.field(),
        this.normalized(),
      );
      if (!this.destroy.destroyed) {
        this.baseline.set(result);
        this.draft.set(result[this.field()] ?? '');
        this.latest.set(null);
        this.saved.emit(result);
      }
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.failed.set(true);
        this.conflict.set(error instanceof ServiceError && error.code === 'revision_conflict');
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.conflict()
            ? 'This reference changed. Review the latest value before saving.'
            : this.unavailable()
              ? 'This reference is no longer available. Your text is still here.'
              : 'Your change could not be saved. It is still here. Try again.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
