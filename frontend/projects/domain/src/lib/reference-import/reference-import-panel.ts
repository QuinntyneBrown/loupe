import {
  afterNextRender,
  untracked,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import {
  REFERENCE_IMPORT_SERVICE,
  REFERENCE_SERVICE,
  OperationResult,
  ReferenceImportRequest,
  ReferenceResult,
  ServiceError,
} from 'api';
@Component({
  selector: 'lp-reference-import-panel',
  templateUrl: './reference-import-panel.html',
  styleUrl: './reference-import-panel.css',
})
export class ReferenceImportPanel {
  readonly reference = input.required<ReferenceResult>();
  readonly metadataDirty = input(false);
  readonly refreshed = output<ReferenceResult>();
  private readonly id = computed(() => this.reference().id);
  private readonly service = inject(REFERENCE_IMPORT_SERVICE);
  private readonly references = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');
  private timer: ReturnType<typeof setTimeout> | undefined;
  private generation = 0;
  private refreshedOperation: string | null = null;
  readonly operation = signal<OperationResult | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly refreshingFailed = signal(false);
  readonly busy = signal(false);
  readonly reviewing = signal(false);
  readonly error = signal<string | null>(null);
  readonly latest = signal<ReferenceResult | null>(null);
  readonly submission = signal<ReferenceImportRequest | null>(null);
  readonly active = computed(() => ['Queued', 'Running'].includes(this.operation()?.status ?? ''));
  readonly needsReview = computed(() => this.error() === 'revision_conflict');
  readonly blocked = computed(
    () =>
      this.loading() ||
      this.busy() ||
      this.reviewing() ||
      this.loadFailed() ||
      this.needsReview() ||
      this.metadataDirty() ||
      !this.reference().sourceUrl ||
      !!this.operation(),
  );
  readonly message = computed(() => {
    switch (this.error()) {
      case 'analysis_limit':
        return 'Too many operations are active. Your reference is saved. Wait for one to finish, then retry.';
      case 'integration_not_configured':
        return 'Import is not configured. Your reference is saved and you can keep editing it.';
      case 'revision_conflict':
        return 'This reference changed. Review its latest saved source before requesting an import.';
      case 'analysis_active':
        return 'An import is already active for another source or image. Check its status before requesting again.';
      case 'item_unavailable':
        return 'The reference is unavailable. Its import could not be requested.';
      default:
        return 'Import admission was not confirmed. Your reference is saved. Retry to check the same request.';
    }
  });
  constructor() {
    effect((onCleanup) => {
      const id = this.id();
      this.generation++;
      clearTimeout(this.timer);
      this.operation.set(null);
      this.submission.set(null);
      this.error.set(null);
      this.latest.set(null);
      this.loadFailed.set(false);
      this.loading.set(false);
      this.busy.set(false);
      this.refreshedOperation = null;
      untracked(() => void this.load(id, false));
      onCleanup(() => {
        this.generation++;
        clearTimeout(this.timer);
      });
    });
  }
  private valid(generation: number): boolean {
    return !this.destroy.destroyed && generation === this.generation;
  }
  private focus(): void {
    const generation = this.generation;
    afterNextRender(
      () => {
        if (this.valid(generation)) this.heading().nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  private schedule(): void {
    clearTimeout(this.timer);
    if (this.active()) this.timer = setTimeout(() => void this.load(this.id(), false), 4000);
  }
  async load(id = this.id(), focus = true): Promise<void> {
    if (this.loading() || this.busy()) return;
    clearTimeout(this.timer);
    const generation = this.generation;
    this.loading.set(true);
    this.loadFailed.set(false);
    try {
      const operation = await this.service.current(id);
      if (!this.valid(generation)) return;
      this.operation.set(operation);
      if (operation?.status === 'Succeeded' && this.refreshedOperation !== operation.id)
        await this.refreshReference(id, generation, operation.id);
    } catch {
      if (this.valid(generation)) this.loadFailed.set(true);
    } finally {
      if (this.valid(generation)) {
        this.loading.set(false);
        if (focus) this.focus();
        this.schedule();
      }
    }
  }
  private async refreshReference(
    id: string,
    generation: number,
    operationId: string,
  ): Promise<void> {
    this.refreshingFailed.set(false);
    try {
      const reference = await this.references.get(id);
      if (this.valid(generation)) {
        this.refreshedOperation = operationId;
        this.refreshed.emit(reference);
      }
    } catch {
      if (this.valid(generation)) this.refreshingFailed.set(true);
    }
  }
  async request(): Promise<void> {
    if (this.blocked()) return;
    const generation = this.generation,
      id = this.id();
    const request = this.submission() ?? {
      revision: this.reference().revision,
      operationKey: crypto.randomUUID(),
    };
    this.submission.set(request);
    this.busy.set(true);
    this.error.set(null);
    this.focus();
    try {
      const operation = await this.service.request(id, request);
      if (this.valid(generation)) {
        this.operation.set(operation);
        this.schedule();
      }
    } catch (error) {
      if (this.valid(generation))
        this.error.set(error instanceof ServiceError ? error.code : 'request_failed');
    } finally {
      if (this.valid(generation)) this.busy.set(false);
    }
  }
  async review(): Promise<void> {
    if (this.reviewing() || this.busy() || this.metadataDirty()) return;
    const generation = this.generation;
    this.reviewing.set(true);
    try {
      const reference = await this.references.get(this.id());
      if (this.valid(generation)) {
        this.latest.set(reference);
        this.refreshed.emit(reference);
        this.submission.set(null);
        this.error.set(null);
        this.focus();
      }
    } catch {
      if (this.valid(generation)) this.error.set('revision_conflict');
    } finally {
      if (this.valid(generation)) this.reviewing.set(false);
    }
  }
}
