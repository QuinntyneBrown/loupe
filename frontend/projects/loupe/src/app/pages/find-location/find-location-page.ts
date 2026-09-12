import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  LOCATION_SEARCH_SERVICE,
  LocationSearchMode,
  LocationSearchRequest,
  LocationSetting,
  LocationTagFacet,
  ServiceError,
  SHOOT_TYPES,
  ShootType,
  TIMES_OF_DAY,
  TimeOfDay,
} from 'api';
import {
  countLocationSearchFilters,
  EMPTY_LOCATION_SEARCH_FILTERS,
  LocationSearchFilters,
  LocationSearchFilterValue,
} from 'components';
import { LocationSearchResults } from 'domain';
import { LocationSearchFiltersDialog } from '../../dialogs/location-search-filters/location-search-filters-dialog';

const settings: readonly LocationSetting[] = ['Indoor', 'Outdoor', 'Mixed'];

@Component({
  selector: 'lp-find-location-page',
  imports: [
    FormsModule,
    RouterLink,
    LocationSearchFilters,
    LocationSearchResults,
    LocationSearchFiltersDialog,
  ],
  templateUrl: './find-location-page.html',
  styleUrl: './find-location-page.css',
})
export class FindLocationPage {
  readonly query = signal('');
  readonly mode = signal<LocationSearchMode>('keyword');
  readonly filters = signal<LocationSearchFilterValue>(EMPTY_LOCATION_SEARCH_FILTERS);
  readonly started = signal(false);
  readonly fieldErrors = signal<Record<string, string>>({});
  readonly request = signal<LocationSearchRequest | null>(null);
  readonly dialogOpen = signal(false);
  readonly tags = signal<LocationTagFacet[] | null>(null);
  readonly tagsFailed = signal(false);
  readonly shootTypes = SHOOT_TYPES;
  readonly periods = TIMES_OF_DAY;
  readonly modes: readonly { value: LocationSearchMode; label: string }[] = [
    { value: 'keyword', label: 'Keyword' },
    { value: 'meaning', label: 'Meaning' },
  ];
  readonly examples = [
    'Golden hour engagement session with leading lines for two people',
    'Indoor headshots with soft window light',
    'Somewhere a family of six can spread out at midday',
  ];
  readonly queryRequired = computed(
    () => this.started() && this.mode() === 'meaning' && this.query().trim().length === 0,
  );
  readonly filterErrors = computed(() =>
    Object.entries(this.fieldErrors())
      .filter(([field]) => field !== 'query')
      .map(([, message]) => message),
  );
  private readonly service = inject(LOCATION_SEARCH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly results = viewChild(LocationSearchResults);
  private readonly queryInput = viewChild.required<ElementRef<HTMLInputElement>>('queryInput');
  private tagsGeneration = 0;
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.query.set(params.get('q') ?? '');
      this.mode.set(params.get('mode') === 'meaning' ? 'meaning' : 'keyword');
      const people = params.get('people');
      this.filters.set({
        shootTypes: params.getAll('shootTypes'),
        people: people === null || people.trim() === '' ? null : Number(people),
        timesOfDay: params.getAll('timesOfDay'),
        setting: params.get('setting') || null,
        tags: params.getAll('tags'),
      });
      this.started.set(params.has('q') || countLocationSearchFilters(this.filters()) > 0);
      const request = this.validatedRequest();
      this.request.set(this.started() ? request : null);
    });
  }
  private validatedRequest(): LocationSearchRequest | null {
    const query = this.query().normalize('NFC').trim();
    const filters = this.filters();
    const errors: Record<string, string> = {};
    if (Array.from(query).length > 500 || query.includes('\0'))
      errors['query'] = 'Use 500 characters or fewer without null characters.';
    else if (this.mode() === 'meaning' && query.length === 0 && this.started())
      errors['query'] =
        'Describe the shoot to search by meaning. Keyword can browse by filters alone.';
    if (!filters.shootTypes.every((value) => (SHOOT_TYPES as readonly string[]).includes(value)))
      errors['shootTypes'] = 'Choose shoot types from the list.';
    if (!filters.timesOfDay.every((value) => (TIMES_OF_DAY as readonly string[]).includes(value)))
      errors['timesOfDay'] = 'Choose times of day from the list.';
    if (filters.setting && !(settings as readonly string[]).includes(filters.setting))
      errors['setting'] = 'Choose Indoor, Outdoor, or Mixed.';
    if (
      filters.people !== null &&
      (!Number.isInteger(filters.people) || filters.people < 1 || filters.people > 500)
    )
      errors['people'] = 'Enter a people count from 1 to 500.';
    if (filters.tags.length > 10) errors['tags'] = 'Choose up to 10 tags.';
    else if (
      !filters.tags.every((tag) => {
        const length = Array.from(tag.normalize('NFC').trim()).length;
        return length > 0 && length <= 50 && !tag.includes('\0');
      })
    )
      errors['tags'] = 'Use tag names of 1–50 characters without null characters.';
    this.fieldErrors.set(errors);
    if (Object.keys(errors).length) return null;
    return {
      query,
      mode: this.mode(),
      shootTypes: filters.shootTypes as ShootType[],
      people: filters.people,
      timesOfDay: filters.timesOfDay as TimeOfDay[],
      setting: (filters.setting as LocationSetting | null) ?? null,
      tags: filters.tags.map((tag) => tag.normalize('NFC').trim()),
    };
  }
  async submit(): Promise<void> {
    const request = this.validatedRequest();
    if (request && JSON.stringify(this.request()) === JSON.stringify(request)) {
      await this.results()?.load(true);
      return;
    }
    const filters = this.filters();
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q: this.query().normalize('NFC').trim(),
        mode: this.mode() === 'meaning' ? 'meaning' : null,
        shootTypes: filters.shootTypes,
        people: filters.people,
        timesOfDay: filters.timesOfDay,
        setting: filters.setting,
        tags: filters.tags,
      },
    });
  }
  chooseMode(mode: LocationSearchMode): void {
    this.mode.set(mode);
    if (this.started()) void this.submit();
  }
  useKeyword(): void {
    this.chooseMode('keyword');
  }
  changeFilters(filters: LocationSearchFilterValue): void {
    this.filters.set(filters);
    void this.submit();
  }
  clearFilters(): void {
    this.changeFilters(EMPTY_LOCATION_SEARCH_FILTERS);
  }
  focusQuery(): void {
    this.queryInput().nativeElement.focus();
  }
  useExample(query: string): void {
    this.query.set(query);
    void this.submit();
  }
  showValidation(error: ServiceError): void {
    this.fieldErrors.set(
      Object.fromEntries(
        Object.entries(error.errors).map(([field, messages]) => [field, messages.join(' ')]),
      ),
    );
  }
  openFilters(): void {
    this.dialogOpen.set(true);
    void this.loadTags();
  }
  async loadTags(): Promise<void> {
    const generation = ++this.tagsGeneration;
    this.tags.set(null);
    this.tagsFailed.set(false);
    try {
      const tags = await this.service.tags();
      if (!this.destroy.destroyed && generation === this.tagsGeneration) this.tags.set(tags);
    } catch {
      if (!this.destroy.destroyed && generation === this.tagsGeneration) this.tagsFailed.set(true);
    }
  }
  applyFilters(filters: LocationSearchFilterValue): void {
    this.dialogOpen.set(false);
    this.changeFilters(filters);
  }
}
