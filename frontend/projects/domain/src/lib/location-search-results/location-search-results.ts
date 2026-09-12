import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  DOCUMENT,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  untracked,
  viewChild,
  viewChildren,
} from '@angular/core';
import {
  LOCATION_SEARCH_SERVICE,
  LocationSearchItem,
  LocationSearchRequest,
  ServiceError,
} from 'api';
import { LocationResultCard } from 'components';

@Component({
  selector: 'lp-location-search-results',
  imports: [LocationResultCard],
  templateUrl: './location-search-results.html',
  styleUrl: './location-search-results.css',
})
export class LocationSearchResults {
  readonly request = input<LocationSearchRequest | null>(null);
  readonly items = signal<LocationSearchItem[]>([]);
  readonly total = signal(0);
  readonly cursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly failure = signal<ServiceError | null>(null);
  readonly invalidCursor = computed(
    () => this.failure()?.code === 'invalid_request' && !!this.failure()?.errors['cursor']?.length,
  );
  readonly refreshRequired = computed(
    () => this.failure()?.code === 'refresh_required' || this.invalidCursor(),
  );
  readonly unavailable = computed(() => this.failure()?.code === 'search_unavailable');
  readonly keywordRequested = output<void>();
  readonly queryEditRequested = output<void>();
  readonly filtersClearRequested = output<void>();
  readonly filtersRequested = output<void>();
  readonly invalidRequest = output<ServiceError>();
  readonly hasFilters = computed(() => {
    const request = this.request();
    return (
      !!request &&
      (request.shootTypes.length > 0 ||
        request.people !== null ||
        request.timesOfDay.length > 0 ||
        !!request.setting ||
        request.tags.length > 0)
    );
  });
  readonly status = computed(() => {
    const count = this.items().length,
      total = this.total();
    const query = this.request()?.query;
    const suffix = query ? ` for “${query}”` : '';
    if (this.loading())
      return count
        ? `Loading more locations… ${count} of ${total} shown.`
        : 'Searching your locations…';
    if (this.failed())
      return count
        ? `${count} of ${total} locations shown. Results could not be updated.`
        : this.unavailable()
          ? 'Meaning search could not be completed.'
          : 'Search could not be completed.';
    return `${total} ${total === 1 ? 'location' : 'locations'}${suffix}`;
  });
  readonly cards = computed(() => {
    const selected = this.request()?.shootTypes ?? [];
    return this.items().map((item) => ({
      ...item,
      ratings: selected.map((shootType) => ({
        shootType,
        rating:
          item.suitability.find((entry) => entry.shootType === shootType)?.rating ??
          'Cannot assess',
      })),
    }));
  });
  private readonly service = inject(LOCATION_SEARCH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);
  private readonly resultCards = viewChildren(LocationResultCard);
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
