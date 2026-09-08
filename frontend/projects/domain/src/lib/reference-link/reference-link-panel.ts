import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
  afterNextRender,
  Injector,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { REFERENCE_SERVICE, ReferenceResult, ServiceError } from 'api';
import {
  referenceMetadataErrors,
  referenceMetadataFields,
  normalizeReferenceMetadata,
} from '../reference-metadata/reference-metadata-fields';
@Component({
  selector: 'lp-reference-link-panel',
  imports: [FormsModule],
  templateUrl: './reference-link-panel.html',
  styleUrl: './reference-link-panel.css',
})
export class ReferenceLinkPanel {
  readonly saved = output<ReferenceResult>();
  readonly existing = output<ReferenceResult>();
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly duplicateHeading = viewChild<ElementRef<HTMLElement>>('duplicateHeading');
  readonly fields = referenceMetadataFields.map((field) => ({
    ...field,
    label: field.key === 'sourceUrl' ? 'Source URL' : field.label,
  }));
  readonly metadata = signal({ title: '', sourceUrl: '', attribution: '', notes: '' });
  readonly saving = signal(false);
  readonly failure = signal<string | null>(null);
  readonly duplicate = signal<ReferenceResult | null>(null);
  private readonly attempted = signal(false);
  private readonly acknowledged = signal(false);
  private readonly operationKey = crypto.randomUUID();
  readonly dirty = computed(
    () =>
      !this.acknowledged() &&
      (this.attempted() || Object.values(this.metadata()).some((value) => !!value.trim())),
  );
  readonly errors = computed(() => referenceMetadataErrors(this.metadata(), false));
  readonly invalid = computed(
    () => !this.metadata().sourceUrl.trim() || Object.keys(this.errors()).length > 0,
  );
  readonly failureMessage = computed(() =>
    this.failure() === 'operation_conflict'
      ? 'This retry differs from an earlier save. Restore the original fields, or return to Inspiration to start a new save.'
      : this.failure() === 'invalid_request'
        ? 'The link was not saved. Check the source and metadata, then retry.'
        : 'The link save was not confirmed. Your fields are still here. Retry to check whether it was saved.',
  );
  setField(field: keyof ReturnType<typeof this.metadata>, value: string): void {
    this.metadata.update((metadata) => ({ ...metadata, [field]: value }));
  }
  openExisting(reference: ReferenceResult): void {
    this.acknowledged.set(true);
    this.existing.emit(reference);
  }
  async save(): Promise<void> {
    if (this.saving() || this.invalid() || this.duplicate()) return;
    this.saving.set(true);
    this.attempted.set(true);
    this.failure.set(null);
    try {
      const result = await this.service.saveLink({
        ...normalizeReferenceMetadata(this.metadata()),
        operationKey: this.operationKey,
      });
      if (this.destroy.destroyed) return;
      if (result.alreadySaved) {
        this.duplicate.set(result.reference);
        afterNextRender(
          () => {
            if (!this.destroy.destroyed) this.duplicateHeading()?.nativeElement.focus();
          },
          { injector: this.injector },
        );
      } else {
        this.acknowledged.set(true);
        this.saved.emit(result.reference);
      }
    } catch (error) {
      if (!this.destroy.destroyed)
        this.failure.set(error instanceof ServiceError ? error.code : 'request_failed');
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
