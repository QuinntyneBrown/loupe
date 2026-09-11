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
} from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  PHOTOGRAPHER_SERVICE,
  PHOTOGRAPHER_SUMMARY_SERVICE,
  PhotographerResult,
  PhotographerSuggestions,
  OperationResult,
  ServiceError,
} from 'api';

@Component({
  selector: 'lp-photographer-summary',
  imports: [DatePipe],
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
      return `${this.host()} doesn't allow automated reading, so there's no summary. Your notes and tags still make this bookmark searchable.`;
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
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private requestInput: { revision: number; key: string } | null = null;
  constructor() {
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
      this.suggestions.set(suggestions);
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
    if (this.busy() || this.active() || this.metadataDirty() || this.conflicted()) return;
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
