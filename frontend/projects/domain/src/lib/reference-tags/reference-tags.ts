import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { REFERENCE_SERVICE, ReferenceResult, ServiceError } from 'api';

@Component({
  selector: 'lp-reference-tags',
  imports: [FormsModule],
  templateUrl: './reference-tags.html',
  styleUrl: './reference-tags.css',
})
export class ReferenceTags {
  readonly reference = input.required<ReferenceResult>();
  readonly saved = output<ReferenceResult>();
  readonly draft = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly conflicted = signal(false);
  readonly failed = signal(false);
  readonly status = signal('');
  readonly action = signal<{ kind: 'add' | 'remove'; name: string } | null>(null);
  readonly dirty = computed(() => !!this.draft().trim() || this.action() !== null || this.busy());
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private key(name: string): string {
    return name.normalize('NFC').toUpperCase();
  }
  change(value: string): void {
    this.draft.set(value);
    this.action.set(null);
    this.error.set('');
    this.failed.set(false);
    this.conflicted.set(false);
  }
  add(): void {
    const name = this.draft().trim().normalize('NFC');
    if (!name || [...name].length > 50) {
      this.error.set('Enter a tag of 1–50 characters.');
      return;
    }
    this.action.set({ kind: 'add', name });
    void this.save();
  }
  remove(name: string): void {
    this.action.set({ kind: 'remove', name });
    void this.save();
  }
  async save(): Promise<void> {
    const action = this.action();
    if (!action || this.busy() || this.conflicted()) return;
    const reference = this.reference();
    const tags = reference.tags.map(({ name, category }) => ({ name, category }));
    const remaining =
      action.kind === 'remove'
        ? tags.filter((tag) => this.key(tag.name) !== this.key(action.name))
        : tags;
    if (
      action.kind === 'add' &&
      !remaining.some((tag) => this.key(tag.name) === this.key(action.name))
    )
      remaining.push({ name: action.name, category: null });
    if (remaining.length > 50) {
      this.error.set('Use up to 50 active tags.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    this.failed.set(false);
    this.status.set('Saving');
    try {
      const saved = await this.service.setTags(reference.id, reference.revision, remaining);
      if (this.destroy.destroyed) return;
      this.saved.emit(saved);
      this.action.set(null);
      if (action.kind === 'add') this.draft.set('');
      this.status.set('Saved');
      afterNextRender(
        () => {
          if (!this.destroy.destroyed) this.field().nativeElement.focus();
        },
        { injector: this.injector },
      );
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.status.set('');
      this.conflicted.set(error instanceof ServiceError && error.code === 'revision_conflict');
      this.failed.set(!this.conflicted());
      this.error.set(
        this.conflicted()
          ? 'This reference changed. Load the latest tags before retrying your change.'
          : 'Tags could not be saved. Your change is still here. Try again.',
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async reload(): Promise<void> {
    this.busy.set(true);
    try {
      const reference = await this.service.get(this.reference().id);
      if (this.destroy.destroyed) return;
      this.saved.emit(reference);
      this.conflicted.set(false);
      this.failed.set(true);
      this.error.set('Latest tags loaded. Review and retry your change.');
    } catch {
      if (!this.destroy.destroyed)
        this.error.set('The latest tags could not be loaded. Try again.');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
