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
import { REFERENCE_SERVICE, ReferenceMetadata, ReferenceResult, ServiceError } from 'api';
import {
  normalizeReferenceMetadata,
  referenceMetadataErrors,
  referenceMetadataFields,
} from './reference-metadata-fields';

@Component({
  selector: 'lp-reference-metadata-editor',
  imports: [FormsModule],
  templateUrl: './reference-metadata-editor.html',
  styleUrl: './reference-metadata-editor.css',
})
export class ReferenceMetadataEditor {
  readonly reference = input.required<ReferenceResult>();
  readonly saved = output<ReferenceResult>();
  readonly discardRequested = output<() => void>();
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly editButton = viewChild<ElementRef<HTMLElement>>('editButton');
  private readonly firstField = viewChild<ElementRef<HTMLElement>>('firstField');
  private readonly latestHeading = viewChild<ElementRef<HTMLElement>>('latestHeading');
  readonly fields = referenceMetadataFields;
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly reloading = signal(false);
  readonly busy = computed(() => this.saving() || this.reloading());
  readonly failed = signal(false);
  readonly conflicted = signal(false);
  readonly unavailable = signal(false);
  readonly reloadFailed = signal(false);
  readonly draft = signal<ReferenceMetadata>({
    title: '',
    sourceUrl: null,
    attribution: null,
    notes: null,
  });
  private readonly baseline = signal<ReferenceMetadata | null>(null);
  private readonly revision = signal(1);
  readonly latest = signal<ReferenceMetadata | null>(null);
  readonly normalized = computed(() => normalizeReferenceMetadata(this.draft()));
  readonly errors = computed(() => referenceMetadataErrors(this.draft(), true));
  readonly invalid = computed(() => Object.keys(this.errors()).length > 0);
  readonly dirty = computed(
    () =>
      this.editing() &&
      this.fields.some((field) => this.normalized()[field.key] !== this.baseline()?.[field.key]),
  );

  private focus(target: () => ElementRef<HTMLElement> | undefined): void {
    afterNextRender(
      () => {
        if (!this.destroy.destroyed) target()?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  open(): void {
    const reference = this.reference();
    this.draft.set(normalizeReferenceMetadata(reference));
    this.baseline.set(normalizeReferenceMetadata(reference));
    this.revision.set(reference.revision);
    this.failed.set(false);
    this.conflicted.set(false);
    this.unavailable.set(false);
    this.reloadFailed.set(false);
    this.latest.set(null);
    this.editing.set(true);
    this.focus(() => this.firstField());
  }
  close(): void {
    if (this.destroy.destroyed) return;
    this.editing.set(false);
    this.focus(() => this.editButton());
  }
  cancel(): void {
    if (this.busy()) return;
    if (this.dirty()) this.discardRequested.emit(() => this.close());
    else this.close();
  }
  setField(field: keyof ReferenceMetadata, value: string): void {
    this.draft.update((draft) => ({ ...draft, [field]: value }));
  }
  async save(): Promise<void> {
    if (this.busy() || this.conflicted() || this.unavailable() || this.invalid() || !this.dirty())
      return;
    this.saving.set(true);
    this.failed.set(false);
    try {
      const reference = await this.service.update(
        this.reference().id,
        this.revision(),
        this.normalized(),
      );
      if (this.destroy.destroyed) return;
      this.saved.emit(reference);
      this.close();
    } catch (error) {
      if (this.destroy.destroyed) return;
      if (error instanceof ServiceError && error.code === 'revision_conflict') {
        this.conflicted.set(true);
        this.latest.set(null);
      } else if (error instanceof ServiceError && error.code === 'item_unavailable')
        this.unavailable.set(true);
      else this.failed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
  async reloadLatest(): Promise<void> {
    if (this.busy() || !this.conflicted()) return;
    this.reloading.set(true);
    this.reloadFailed.set(false);
    try {
      const reference = await this.service.get(this.reference().id);
      if (this.destroy.destroyed) return;
      this.revision.set(reference.revision);
      this.baseline.set(normalizeReferenceMetadata(reference));
      this.latest.set(normalizeReferenceMetadata(reference));
      this.conflicted.set(false);
      this.unavailable.set(false);
      this.saved.emit(reference);
      this.focus(() => this.latestHeading());
    } catch (error) {
      if (this.destroy.destroyed) return;
      if (error instanceof ServiceError && error.code === 'item_unavailable')
        this.unavailable.set(true);
      else this.reloadFailed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.reloading.set(false);
    }
  }
}
