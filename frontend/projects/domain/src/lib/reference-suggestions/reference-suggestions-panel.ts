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
import { DatePipe, TitleCasePipe } from '@angular/common';
import {
  REFERENCE_ANALYSIS_SERVICE,
  REFERENCE_SERVICE,
  OperationResult,
  ReferenceResult,
  ReferenceSuggestions,
  ReferenceSuggestionReview,
  ServiceError,
} from 'api';

@Component({
  selector: 'lp-reference-suggestions',
  imports: [DatePipe, TitleCasePipe],
  templateUrl: './reference-suggestions-panel.html',
  styleUrl: './reference-suggestions-panel.css',
})
export class ReferenceSuggestionsPanel {
  readonly reference = input.required<ReferenceResult>();
  readonly metadataDirty = input(false);
  readonly saved = output<ReferenceResult>();
  private readonly service = inject(REFERENCE_ANALYSIS_SERVICE);
  private readonly references = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private requestKey: string | null = null;
  private requestRevision = 0;
  private undoSnapshot: ReferenceSuggestions | null = null;
  readonly suggestions = signal<ReferenceSuggestions | null>(null);
  readonly operation = signal<OperationResult | null>(null);
  readonly description = signal('');
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly failureCode = signal('');
  readonly decision = signal<ReferenceSuggestionReview | null>(null);
  readonly expanded = signal(false);
  readonly notice = signal('');
  readonly undoRevision = signal<number | null>(null);
  readonly active = computed(() => ['Queued', 'Running'].includes(this.operation()?.status ?? ''));
  readonly pending = computed(
    () =>
      this.suggestions()?.descriptionState === 'pending' ||
      this.suggestions()?.tags.some((tag) => tag.state === 'pending'),
  );
  readonly acceptedCount = computed(
    () => this.suggestions()?.tags.filter((tag) => tag.state === 'accepted').length ?? 0,
  );
  readonly groups = computed(() =>
    ['subject', 'genre', 'lighting', 'composition', 'palette', 'mood', 'technique']
      .map((category) => ({
        category,
        tags:
          this.suggestions()?.tags.filter(
            (tag) => tag.category === category && tag.state === 'pending',
          ) ?? [],
      }))
      .filter((group) => group.tags.length),
  );
  readonly dirty = computed(
    () =>
      this.busy() ||
      !!this.decision() ||
      (this.suggestions()?.descriptionState === 'pending' &&
        this.description().trim() !== this.suggestions()?.description),
  );
  readonly descriptionInvalid = computed(
    () => !this.description().trim() || [...this.description().trim()].length > 4000,
  );
  readonly blocked = computed(
    () =>
      this.busy() ||
      this.metadataDirty() ||
      this.loading() ||
      this.loadFailed() ||
      !!this.decision() ||
      this.failureCode() === 'item_unavailable',
  );
  readonly identity = computed(() => this.reference().id + ':' + this.reference().imageUrl);
  constructor() {
    effect((cleanup) => {
      this.identity();
      const generation = ++this.generation;
      untracked(() => {
        this.suggestions.set(null);
        this.operation.set(null);
        this.description.set('');
        this.decision.set(null);
        this.error.set('');
        this.failureCode.set('');
        this.notice.set('');
        this.undoRevision.set(null);
        this.undoSnapshot = null;
        this.requestKey = null;
        this.busy.set(false);
        this.expanded.set(false);
        void this.load(generation);
      });
      cleanup(() => {
        this.generation++;
        clearTimeout(this.timer);
      });
    });
  }
  private valid(generation: number): boolean {
    return generation === this.generation && !this.destroy.destroyed;
  }
  async load(generation = this.generation): Promise<void> {
    clearTimeout(this.timer);
    this.loading.set(!this.operation());
    this.loadFailed.set(false);
    try {
      const id = this.reference().id;
      const [operation, suggestions] = await Promise.all([
        this.service.current(id),
        this.service.suggestions(id),
      ]);
      if (!this.valid(generation)) return;
      const changed = suggestions?.operationId !== this.suggestions()?.operationId;
      this.operation.set(operation);
      if (changed || !this.dirty()) {
        this.suggestions.set(suggestions);
        this.description.set(suggestions?.description ?? '');
      }
      if (changed && suggestions) {
        const latest = await this.references.get(id);
        if (this.valid(generation)) this.saved.emit(latest);
      }
    } catch {
      if (this.valid(generation)) this.loadFailed.set(true);
    } finally {
      if (this.valid(generation)) {
        this.loading.set(false);
        if (this.active() && !this.loadFailed())
          this.timer = setTimeout(() => void this.load(generation), 1500);
      }
    }
  }
  async generate(): Promise<void> {
    if (this.blocked() || this.active() || !this.reference().imageUrl) return;
    const generation = this.generation;
    if (!this.requestKey) {
      this.requestKey = crypto.randomUUID();
      this.requestRevision = this.reference().revision;
    }
    this.busy.set(true);
    this.error.set('');
    this.failureCode.set('');
    try {
      const operation = await this.service.request(
        this.reference().id,
        this.requestRevision,
        this.requestKey,
      );
      if (this.valid(generation)) {
        this.operation.set(operation);
        this.requestKey = null;
        this.timer = setTimeout(() => void this.load(generation), 1500);
      }
    } catch (error) {
      if (this.valid(generation))
        this.fail(
          error,
          'The analysis request was not confirmed. Try again to check the same request.',
        );
    } finally {
      if (this.valid(generation)) this.busy.set(false);
    }
  }
  async review(
    target: ReferenceSuggestionReview['target'],
    decision: ReferenceSuggestionReview['decision'],
    name?: string,
  ): Promise<void> {
    if (this.blocked() || !this.suggestions()) return;
    if (
      decision === 'accept' &&
      target !== 'tag' &&
      this.suggestions()?.descriptionState === 'pending' &&
      this.descriptionInvalid()
    )
      return;
    this.decision.set({
      target,
      decision,
      name,
      operationId: this.suggestions()!.operationId,
      revision: this.reference().revision,
      ...(target !== 'tag' && decision === 'accept' ? { value: this.description().trim() } : {}),
    });
    await this.retryReview();
  }
  async retryReview(): Promise<void> {
    const decision = this.decision();
    if (
      !decision ||
      this.busy() ||
      this.metadataDirty() ||
      this.failureCode() === 'revision_conflict' ||
      this.failureCode() === 'item_unavailable'
    )
      return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.failureCode.set('');
    try {
      const reference = await this.service.review(this.reference().id, decision);
      if (!this.valid(generation)) return;
      const previous = this.suggestions()!;
      const state = decision.decision === 'accept' ? 'accepted' : 'dismissed';
      this.undoSnapshot = structuredClone(previous);
      this.suggestions.set({
        ...previous,
        descriptionState:
          previous.descriptionState === 'pending' && decision.target !== 'tag'
            ? state
            : previous.descriptionState,
        tags: previous.tags.map((tag) =>
          tag.state === 'pending' &&
          (decision.target === 'all' || (decision.target === 'tag' && decision.name === tag.name))
            ? { ...tag, state }
            : tag,
        ),
      });
      this.undoRevision.set(reference.revision);
      this.decision.set(null);
      this.saved.emit(reference);
      this.notice.set(
        decision.decision === 'dismiss'
          ? 'Suggestions dismissed.'
          : decision.target === 'all'
            ? 'Suggestions applied.'
            : 'Suggestion applied.',
      );
    } catch (error) {
      if (this.valid(generation))
        this.fail(error, 'Your decision has not been confirmed. Retry to save it.');
    } finally {
      if (this.valid(generation)) this.busy.set(false);
    }
  }
  async undo(): Promise<void> {
    const revision = this.undoRevision();
    if (!revision || this.busy() || this.metadataDirty()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.failureCode.set('');
    try {
      const reference = await this.service.undo(this.reference().id, revision);
      if (!this.valid(generation)) return;
      this.suggestions.set(this.undoSnapshot);
      this.description.set(this.undoSnapshot?.description ?? '');
      this.undoSnapshot = null;
      this.undoRevision.set(null);
      this.notice.set('Review undone.');
      this.saved.emit(reference);
    } catch (error) {
      if (this.valid(generation)) this.fail(error, 'Undo was not confirmed. Try Undo again.');
    } finally {
      if (this.valid(generation)) this.busy.set(false);
    }
  }
  async reviewLatest(): Promise<void> {
    if (this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    try {
      const [reference, suggestions] = await Promise.all([
        this.references.get(this.reference().id),
        this.service.suggestions(this.reference().id),
      ]);
      if (!this.valid(generation)) return;
      const decision = this.decision();
      const stillPending =
        decision &&
        suggestions?.operationId === decision.operationId &&
        (decision.target === 'description'
          ? suggestions.descriptionState === 'pending'
          : decision.target === 'tag'
            ? suggestions.tags.some((tag) => tag.name === decision.name && tag.state === 'pending')
            : suggestions.descriptionState === 'pending' ||
              suggestions.tags.some((tag) => tag.state === 'pending'));
      this.decision.set(stillPending ? { ...decision, revision: reference.revision } : null);
      this.suggestions.set(suggestions);
      this.saved.emit(reference);
      this.requestKey = null;
      this.undoRevision.set(null);
      this.error.set('');
      this.failureCode.set('');
      this.notice.set(
        stillPending
          ? 'Latest saved metadata loaded. Your decision is ready to retry.'
          : 'Latest saved metadata loaded.',
      );
      if (!stillPending) this.description.set(suggestions?.description ?? '');
    } catch (error) {
      if (this.valid(generation))
        this.fail(error, 'The latest metadata could not be loaded. Try again.');
    } finally {
      if (this.valid(generation)) this.busy.set(false);
    }
  }
  private fail(error: unknown, fallback: string): void {
    const code = error instanceof ServiceError ? error.code : 'request_failed';
    this.failureCode.set(code);
    this.error.set(
      code === 'revision_conflict'
        ? 'This reference changed. Review the latest saved metadata before continuing.'
        : code === 'item_unavailable'
          ? 'This reference is unavailable.'
          : code === 'analysis_limit'
            ? 'Too many operations are active. Wait for one to finish, then try again.'
            : code === 'integration_not_configured'
              ? 'Image analysis is not configured. Your reference is saved.'
              : code === 'invalid_request'
                ? 'Check the description and tags. A reference can have at most 50 active tags.'
                : fallback,
    );
  }
}
