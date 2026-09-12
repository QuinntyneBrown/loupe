import {
  afterNextRender,
  Component,
  DestroyRef,
  DOCUMENT,
  ElementRef,
  computed,
  effect,
  inject,
  Injector,
  input,
  output,
  signal,
  untracked,
  viewChild,
  viewChildren,
} from '@angular/core';
import { SEARCH_SERVICE, SearchItem, SearchRequest, ServiceError } from 'api';
import { ReferenceCard, PhotographerCard } from 'components';

@Component({
  selector: 'lp-search-results',
  imports: [ReferenceCard, PhotographerCard],
  templateUrl: './search-results.html',
  styleUrl: './search-results.css',
})
export class SearchResults {
  readonly request = input<SearchRequest | null>(null);
  readonly items = signal<SearchItem[]>([]);
  readonly total = signal(0);
  readonly cursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly failure = signal<ServiceError | null>(null);
  readonly invalidCursor = computed(
    () => this.failure()?.code === 'invalid_request' && !!this.failure()?.errors['cursor']?.length,
  );
  readonly filtersRequested = output<void>();
  readonly queryEditRequested = output<void>();
  readonly filtersClearRequested = output<void>();
  readonly invalidRequest = output<ServiceError>();
  readonly status = computed(() => {
    const count = this.items().length;
    const retained = `${count} of ${this.total()} results retained.`;
    if (this.loading())
      return count ? `Loading more results… ${retained}` : 'Searching your library…';
    if (this.failed())
      return count ? `${retained} Results could not be updated.` : 'Search could not be completed.';
    const query = this.request()?.query;
    return `Showing ${count} of ${this.total()} results${query ? ' for “' + query + '”' : ''}.`;
  });
  readonly cards = computed(() =>
    this.items().map((item) => ({
      ...item,
      references: item.referencePreviewUrls.map((url, index) => ({
        id: `${index}:${url}`,
        title: `Linked reference ${index + 1}`,
        previewUrl: url,
      })),
    })),
  );
  private readonly service = inject(SEARCH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);
  private readonly resultCards = viewChildren<ReferenceCard | PhotographerCard>('resultCard');
  private readonly emptyHeading = viewChild<ElementRef<HTMLElement>>('emptyHeading');
  private generation = 0;
  private pendingRequest: string | null = null;
  constructor() {
    effect(() => {
      this.request();
      untracked(() => void this.load(true));
    });
    this.destroy.onDestroy(() => this.generation++);
  }
  async load(reset = false, focus = false): Promise<void> {
    const request = this.request(),
      key = JSON.stringify(request);
    if (this.loading() && this.pendingRequest === key) return;
    if (!reset && this.items().length && !this.cursor()) return;
    const previousCount = reset ? 0 : this.items().length;
    const focusOrigin = this.document.activeElement;
    this.pendingRequest = key;
    const generation = ++this.generation;
    if (reset) {
      this.items.set([]);
      this.total.set(0);
      this.cursor.set(null);
    }
    this.failed.set(false);
    this.failure.set(null);
    if (!request) {
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    try {
      const result = await this.service.search({
        ...request,
        cursor: reset ? undefined : (this.cursor() ?? undefined),
      });
      if (this.destroy.destroyed || generation !== this.generation) return;
      this.items.update((items) => (reset ? result.items : [...items, ...result.items]));
      this.total.set(result.totalCount);
      this.cursor.set(result.nextCursor);
      if (focus)
        afterNextRender(
          () => {
            if (this.destroy.destroyed || generation !== this.generation) return;
            const active = this.document.activeElement;
            if (active !== focusOrigin && active !== this.document.body) return;
            const cards = this.resultCards();
            const card = cards[Math.min(previousCount, cards.length - 1)];
            if (card) card.focus();
            else this.emptyHeading()?.nativeElement.focus();
          },
          { injector: this.injector },
        );
    } catch (error) {
      if (!this.destroy.destroyed && generation === this.generation) {
        const failure = error instanceof ServiceError ? error : new ServiceError('request_failed');
        this.failure.set(failure);
        this.failed.set(true);
        if (failure.code === 'invalid_request' && !this.invalidCursor())
          this.invalidRequest.emit(failure);
      }
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
}
