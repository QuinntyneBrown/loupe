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
import { TitleCasePipe } from '@angular/common';
import { REFERENCE_SERVICE, ReferenceResult, ReferenceTag, ServiceError } from 'api';

@Component({
  selector: 'lp-reference-tags',
  imports: [FormsModule, TitleCasePipe],
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
  readonly editing = signal<ReferenceTag | null>(null);
  readonly editName = signal('');
  readonly editCategory = signal('');
  private editRevision = 0;
  readonly categories = [
    'subject',
    'genre',
    'lighting',
    'composition',
    'palette',
    'mood',
    'technique',
  ];
  readonly editInvalid = computed(
    () => !this.editName().trim() || [...this.editName().trim().normalize('NFC')].length > 50,
  );
  readonly action = signal<{
    kind: 'add' | 'remove' | 'edit';
    name: string;
    revision: number;
    originalName?: string;
    category?: string | null;
  } | null>(null);
  readonly dirty = computed(
    () => !!this.editing() || !!this.draft().trim() || this.action() !== null || this.busy(),
  );
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private readonly editField = viewChild<ElementRef<HTMLInputElement>>('editField');
  edit(tag: ReferenceTag): void {
    if (this.busy() || this.action() || this.editing()) return;
    this.editing.set(tag);
    this.editName.set(tag.name);
    this.editCategory.set(tag.category ?? '');
    this.editRevision = this.reference().revision;
    this.error.set('');
    this.failed.set(false);
    this.conflicted.set(false);
    afterNextRender(() => this.editField()?.nativeElement.focus(), { injector: this.injector });
  }
  editChanged(): void {
    this.action.set(null);
    this.failed.set(false);
    this.error.set('');
  }
  cancelEdit(): void {
    if (this.busy()) return;
    this.editing.set(null);
    this.action.set(null);
    this.error.set('');
    this.failed.set(false);
    this.conflicted.set(false);
    afterNextRender(() => this.field().nativeElement.focus(), { injector: this.injector });
  }
  saveEdit(): void {
    if (!this.editing() || this.editInvalid() || this.busy() || this.conflicted()) return;
    this.action.set({
      kind: 'edit',
      originalName: this.editing()!.name,
      name: this.editName().trim().normalize('NFC'),
      category: this.editCategory() || null,
      revision: this.editRevision,
    });
    void this.save();
  }
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
    if (this.editing() || this.busy()) return;
    const name = this.draft().trim().normalize('NFC');
    if (!name || [...name].length > 50) {
      this.error.set('Enter a tag of 1–50 characters.');
      return;
    }
    this.action.set({ kind: 'add', name, revision: this.reference().revision });
    void this.save();
  }
  remove(name: string): void {
    if (this.editing() || this.busy()) return;
    this.action.set({ kind: 'remove', name, revision: this.reference().revision });
    void this.save();
  }
  async save(): Promise<void> {
    const action = this.action();
    if (!action || this.busy() || this.conflicted()) return;
    const reference = this.reference();
    const tags = reference.tags.map(({ name, category }) => ({ name, category }));
    const remaining =
      action.kind === 'remove' || action.kind === 'edit'
        ? tags.filter((tag) => this.key(tag.name) !== this.key(action.originalName ?? action.name))
        : tags;
    if (action.kind === 'edit') {
      if (!tags.some((tag) => this.key(tag.name) === this.key(action.originalName!))) {
        this.error.set('This tag was removed. Cancel the edit or add a new tag.');
        return;
      }
      if (remaining.some((tag) => this.key(tag.name) === this.key(action.name))) {
        this.error.set('That tag already exists. Choose another name.');
        return;
      }
      remaining.push({ name: action.name, category: action.category ?? null });
    }
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
      const saved = await this.service.setTags(reference.id, action.revision, remaining);
      if (this.destroy.destroyed) return;
      this.saved.emit(saved);
      this.action.set(null);
      this.editing.set(null);
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
      this.action.update((action) => (action ? { ...action, revision: reference.revision } : null));
      this.editRevision = reference.revision;
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
