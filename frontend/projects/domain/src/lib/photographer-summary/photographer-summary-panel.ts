import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  afterNextRender,
  ElementRef,
  Injector,
  viewChild,
} from '@angular/core';
import { DatePipe, TitleCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  PHOTOGRAPHER_SERVICE,
  PHOTOGRAPHER_SUMMARY_SERVICE,
  PhotographerResult,
  PhotographerSuggestions,
  OperationResult,
  ServiceError,
  PhotographerSuggestedTag,
  PhotographerSuggestionReview,
} from 'api';

@Component({
  selector: 'lp-photographer-summary',
  imports: [DatePipe, TitleCasePipe, FormsModule],
  templateUrl: './photographer-summary-panel.html',
  styleUrl: './photographer-summary-panel.css',
})
export class PhotographerSummaryPanel {
  readonly photographer = input.required<PhotographerResult>();
  readonly metadataDirty = input(false);
  readonly saved = output<PhotographerResult>();
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly operation = signal<OperationResult | null>(null);
  readonly suggestions = signal<PhotographerSuggestions | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly conflicted = signal(false);
  readonly summaryDraft = signal('');
  readonly editingTag = signal<PhotographerSuggestedTag | null>(null);
  readonly tagName = signal('');
  readonly tagCategory = signal('');
  readonly notice = signal('');
  readonly undoRevision = signal<number | null>(null);
  readonly retryWaiting = signal(false);
  readonly categories = [
    'subject',
    'genre',
    'lighting',
    'composition',
    'palette',
    'mood',
    'technique',
  ];
  readonly summaryEdited = computed(
    () =>
      this.suggestions()?.summaryStatus === 'pending' &&
      this.summaryDraft().trim() !== this.suggestions()?.summary?.trim(),
  );
  readonly summaryInvalid = computed(
    () =>
      !this.summaryDraft().trim() ||
      Array.from(this.summaryDraft().trim()).length > 4000 ||
      this.summaryDraft().includes('\0'),
  );
  readonly tagInvalid = computed(
    () =>
      !this.tagName().trim() ||
      Array.from(this.tagName().trim().normalize('NFC')).length > 50 ||
      this.tagName().includes('\0'),
  );
  readonly dirty = computed(() => this.busy() || this.summaryEdited() || !!this.editingTag());
  readonly pending = computed(
    () =>
      this.suggestions()?.summaryStatus === 'pending' ||
      this.suggestions()?.tags.some((tag) => tag.state === 'pending'),
  );
  readonly groups = computed(() =>
    this.categories
      .map((category) => ({
        category,
        tags:
          this.suggestions()?.tags.filter(
            (tag) => tag.category === category && tag.state === 'pending',
          ) ?? [],
      }))
      .filter((group) => group.tags.length),
  );
  readonly reviewDisabled = computed(
    () =>
      this.busy() ||
      this.metadataDirty() ||
      this.previous() ||
      this.active() ||
      this.conflicted() ||
      this.loading(),
  );
  readonly active = computed(() => ['Queued', 'Running'].includes(this.operation()?.status ?? ''));
  readonly host = computed(() => new URL(this.photographer().portfolioUrl).hostname);
  readonly previous = computed(
    () =>
      !!this.suggestions() &&
      this.suggestions()!.sourceRevision !== this.photographer().sourceRevision,
  );
  readonly unavailable = computed(() => {
    if (this.suggestions()?.unavailableReason === 'insufficient_information' && !this.previous())
      return 'This page does not contain enough information for a summary. Your notes and tags still make this bookmark searchable.';
    const code = this.operation()?.failureCode;
    if (code === 'robots_disallowed')
      return `${this.host()} doesn't allow automated reading, so there's no generated summary. Your notes and tags still make this bookmark searchable.`;
    if (code === 'source_access_denied')
      return 'This page requires access Loupe does not have. Your notes and tags still make this bookmark searchable.';
    if (code?.startsWith('source_') || code?.startsWith('robots_'))
      return 'This portfolio page could not be read. Your notes and tags still make this bookmark searchable.';
    if (
      ['provider_disabled', 'provider_credentials', 'provider_access_denied'].includes(code ?? '')
    )
      return 'Summary generation is unavailable. You can still edit your description, notes and tags.';
    return '';
  });
  private readonly service = inject(PHOTOGRAPHER_SUMMARY_SERVICE);
  private readonly photographers = inject(PHOTOGRAPHER_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly tagField = viewChild<ElementRef<HTMLInputElement>>('tagField');
  private focus(element: 'heading' | 'tag' = 'heading'): void {
    afterNextRender(
      () => {
        if (!this.destroy.destroyed)
          (element === 'tag' ? this.tagField() : this.heading())?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  editTag(tag: PhotographerSuggestedTag): void {
    this.editingTag.set(tag);
    this.tagName.set(tag.name);
    this.tagCategory.set(tag.category);
    this.focus('tag');
  }
  cancelTag(): void {
    this.editingTag.set(null);
    this.focus();
  }
  async review(
    target: 'summary' | 'tag' | 'all',
    decision: 'accept' | 'dismiss',
    tag?: PhotographerSuggestedTag,
    edited = false,
  ): Promise<void> {
    const value = this.suggestions();
    if (
      !value ||
      this.reviewDisabled() ||
      (decision === 'accept' &&
        (target === 'summary' || target === 'all') &&
        value.summaryStatus === 'pending' &&
        this.summaryInvalid()) ||
      (edited && this.tagInvalid())
    )
      return;
    const input: PhotographerSuggestionReview = {
      operationId: value.operationId,
      revision: this.photographer().revision,
      target,
      decision,
      name: tag?.name,
      value:
        target === 'summary' || target === 'all'
          ? this.summaryDraft()
          : edited
            ? this.tagName().trim().normalize('NFC')
            : undefined,
      category: edited ? this.tagCategory() : undefined,
    };
    const generation = this.generation,
      id = this.photographer().id;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const saved = await this.service.review(id, input);
      if (this.destroy.destroyed || generation !== this.generation) return;
      this.saved.emit(saved);
      this.undoRevision.set(saved.revision);
      if (target === 'tag') this.editingTag.set(null);
      await this.load();
      this.notice.set(
        `${target === 'summary' ? 'Summary' : target === 'tag' ? 'Tag' : 'Suggestions'} ${decision === 'accept' ? 'accepted' : 'dismissed'}.`,
      );
      this.focus();
    } catch (error) {
      if (!this.destroy.destroyed && generation === this.generation) {
        this.conflicted.set(error instanceof ServiceError && error.code === 'revision_conflict');
        this.error.set(
          this.conflicted()
            ? 'This bookmark changed. Review its latest details before saving your suggestion edits.'
            : "Couldn't save this review. Your edits are kept. Try the action again.",
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async undo(): Promise<void> {
    const revision = this.undoRevision();
    if (revision === null || this.busy() || this.metadataDirty()) return;
    const generation = this.generation,
      id = this.photographer().id;
    this.busy.set(true);
    this.error.set('');
    try {
      const saved = await this.service.undo(id, revision);
      if (this.destroy.destroyed || generation !== this.generation) return;
      this.saved.emit(saved);
      this.undoRevision.set(null);
      await this.load();
      this.notice.set('Review undone.');
      this.focus();
    } catch {
      if (!this.destroy.destroyed)
        this.error.set(
          "Couldn't undo this review. The bookmark may have changed; your saved content is kept.",
        );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private requestInput: { revision: number; key: string } | null = null;
  constructor() {
    effect((onCleanup) => {
      const available = this.operation()?.retryAvailableAt;
      const delay = available ? Date.parse(available) - Date.now() : 0;
      this.retryWaiting.set(delay > 0);
      if (delay > 0) {
        const timer = setTimeout(() => this.retryWaiting.set(false), delay);
        onCleanup(() => clearTimeout(timer));
      }
    });
    let previous = '';
    effect(() => {
      const item = this.photographer();
      const key = `${item.id}:${item.sourceRevision}`;
      if (key === previous) return;
      previous = key;
      untracked(() => {
        this.generation++;
        clearTimeout(this.timer);
        this.requestInput = null;
        this.operation.set(null);
        this.suggestions.set(null);
        void this.load();
      });
    });
    this.destroy.onDestroy(() => {
      this.generation++;
      clearTimeout(this.timer);
    });
  }
  async load(): Promise<void> {
    const generation = ++this.generation,
      id = this.photographer().id;
    clearTimeout(this.timer);
    this.loading.set(true);
    this.loadFailed.set(false);
    try {
      const [operation, suggestions] = await Promise.all([
        this.service.current(id),
        this.service.suggestions(id),
      ]);
      if (this.destroy.destroyed || generation !== this.generation) return;
      const completed =
        operation?.status === 'Succeeded' && this.operation()?.status !== 'Succeeded';
      this.operation.set(operation);
      const keepDraft = this.summaryEdited();
      this.suggestions.set(suggestions);
      if (!keepDraft) this.summaryDraft.set(suggestions?.summary ?? '');
      if (completed && !this.metadataDirty()) {
        const latest = await this.photographers.get(id);
        if (this.destroy.destroyed || generation !== this.generation) return;
        this.saved.emit(latest);
      }
      if (this.active()) this.timer = setTimeout(() => void this.load(), 1000);
    } catch {
      if (!this.destroy.destroyed && generation === this.generation) this.loadFailed.set(true);
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
  async request(): Promise<void> {
    if (
      this.busy() ||
      this.retryWaiting() ||
      this.active() ||
      this.metadataDirty() ||
      this.conflicted() ||
      this.summaryEdited() ||
      this.editingTag()
    )
      return;
    this.requestInput ??= { revision: this.photographer().revision, key: crypto.randomUUID() };
    this.busy.set(true);
    this.error.set('');
    try {
      const operation = await this.service.request(
        this.photographer().id,
        this.requestInput.revision,
        this.requestInput.key,
      );
      if (this.destroy.destroyed) return;
      this.operation.set(operation);
      this.requestInput = null;
      await this.load();
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.conflicted.set(error instanceof ServiceError && error.code === 'revision_conflict');
        this.error.set(
          this.conflicted()
            ? 'This bookmark changed. Review its latest details before requesting another summary.'
            : "Couldn't request a summary. Your saved content is unchanged. Try again.",
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async reviewLatest(): Promise<void> {
    if (this.busy() || this.metadataDirty()) return;
    this.busy.set(true);
    try {
      const latest = await this.photographers.get(this.photographer().id);
      if (this.destroy.destroyed) return;
      this.saved.emit(latest);
      this.requestInput = null;
      this.conflicted.set(false);
      this.error.set('');
      await this.load();
    } catch {
      if (!this.destroy.destroyed)
        this.error.set("Couldn't load the latest bookmark. Your saved content is unchanged.");
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
