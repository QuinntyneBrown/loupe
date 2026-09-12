import { Component, computed, input, output } from '@angular/core';

export interface LocationSearchFilterValue {
  shootTypes: string[];
  people: number | null;
  timesOfDay: string[];
  setting: string | null;
  tags: string[];
}
export const EMPTY_LOCATION_SEARCH_FILTERS: LocationSearchFilterValue = {
  shootTypes: [],
  people: null,
  timesOfDay: [],
  setting: null,
  tags: [],
};
export function countLocationSearchFilters(value: LocationSearchFilterValue): number {
  return (
    value.shootTypes.length +
    (value.people === null ? 0 : 1) +
    value.timesOfDay.length +
    (value.setting ? 1 : 0) +
    value.tags.length
  );
}

let instances = 0;

@Component({
  selector: 'lp-location-search-filters',
  templateUrl: './location-search-filters.html',
  styleUrl: './location-search-filters.css',
})
export class LocationSearchFilters {
  readonly shootTypeOptions = input.required<readonly string[]>();
  readonly periodOptions = input.required<readonly string[]>();
  readonly settingOptions: readonly string[] = ['Indoor', 'Outdoor', 'Mixed'];
  readonly value = input<LocationSearchFilterValue>(EMPTY_LOCATION_SEARCH_FILTERS);
  readonly valueChange = output<LocationSearchFilterValue>();
  readonly dialogRequested = output<void>();
  readonly id = `location-search-filters-${++instances}`;
  readonly count = computed(() => countLocationSearchFilters(this.value()));
  readonly summary = computed(() => {
    const count = this.count();
    return count ? `${count} ${count === 1 ? 'filter' : 'filters'}` : 'Filters';
  });
  toggleShootType(shootType: string): void {
    this.valueChange.emit({
      ...this.value(),
      shootTypes: this.toggled(this.value().shootTypes, shootType),
    });
  }
  togglePeriod(period: string): void {
    this.valueChange.emit({
      ...this.value(),
      timesOfDay: this.toggled(this.value().timesOfDay, period),
    });
  }
  setPeople(raw: string): void {
    const people = raw.trim() === '' ? null : Number(raw);
    if (people !== null && !Number.isInteger(people)) return;
    if (people === this.value().people) return;
    this.valueChange.emit({ ...this.value(), people });
  }
  chooseSetting(setting: string | null): void {
    this.valueChange.emit({ ...this.value(), setting });
  }
  clear(): void {
    this.valueChange.emit(EMPTY_LOCATION_SEARCH_FILTERS);
  }
  private toggled(values: string[], value: string): string[] {
    return values.includes(value) ? values.filter((other) => other !== value) : [...values, value];
  }
}
