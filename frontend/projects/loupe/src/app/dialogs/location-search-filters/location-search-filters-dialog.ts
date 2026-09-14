import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { LocationTagFacet } from 'api';
import { LocationSearchFilterValue } from 'components';

@Component({
  selector: 'lp-location-search-filters-dialog',
  templateUrl: './location-search-filters-dialog.html',
  styleUrl: './location-search-filters-dialog.css',
})
export class LocationSearchFiltersDialog {
  readonly shootTypeOptions = input.required<readonly string[]>();
  readonly periodOptions = input.required<readonly string[]>();
  readonly tags = input.required<LocationTagFacet[] | null>();
  readonly tagsFailed = input(false);
  readonly value = input.required<LocationSearchFilterValue>();
  readonly retryTags = output<void>();
  readonly applied = output<LocationSearchFilterValue>();
  readonly closed = output<void>();
  readonly settingOptions: readonly string[] = ['Indoor', 'Outdoor', 'Mixed'];
  readonly draftShootTypes = signal<string[]>([]);
  readonly draftPeople = signal('');
  readonly draftPeriods = signal<string[]>([]);
  readonly draftSetting = signal<string | null>(null);
  readonly draftTags = signal<string[]>([]);
  readonly tagError = signal('');
  readonly peopleError = signal('');
  readonly tagChoices = computed(() => {
    const known = (this.tags() ?? []).map((tag) => ({ name: tag.name, count: tag.count }));
    const extra = this.value()
      .tags.filter((tag) => !known.some((choice) => this.key(choice.name) === this.key(tag)))
      .map((name) => ({ name, count: null }));
    return [...known, ...extra];
  });
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private readonly destroy = inject(DestroyRef);
  constructor() {
    afterNextRender(() => {
      const value = this.value();
      this.draftShootTypes.set([...value.shootTypes]);
      this.draftPeople.set(value.people === null ? '' : String(value.people));
      this.draftPeriods.set([...value.timesOfDay]);
      this.draftSetting.set(value.setting);
      this.draftTags.set([...value.tags]);
      this.modal().nativeElement.showModal();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  private key(name: string): string {
    return name.normalize('NFC').trim().toUpperCase();
  }
  hasTag(name: string): boolean {
    return this.draftTags().some((tag) => this.key(tag) === this.key(name));
  }
  toggleShootType(shootType: string): void {
    this.draftShootTypes.update((values) => this.toggled(values, shootType));
  }
  togglePeriod(period: string): void {
    this.draftPeriods.update((values) => this.toggled(values, period));
  }
  toggleTag(name: string): void {
    this.tagError.set('');
    if (!this.hasTag(name) && this.draftTags().length >= 10) {
      this.tagError.set('Choose up to 10 tags.');
      return;
    }
    this.draftTags.update((tags) =>
      this.hasTag(name) ? tags.filter((tag) => this.key(tag) !== this.key(name)) : [...tags, name],
    );
  }
  clear(): void {
    this.draftShootTypes.set([]);
    this.draftPeople.set('');
    this.draftPeriods.set([]);
    this.draftSetting.set(null);
    this.draftTags.set([]);
    this.tagError.set('');
    this.peopleError.set('');
  }
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const controls = this.modal().nativeElement.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled])',
    );
    const first = controls[0],
      last = controls[controls.length - 1];
    if (event.shiftKey ? event.target === first : event.target === last) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  apply(): void {
    const raw = this.draftPeople().trim();
    const people = raw === '' ? null : Number(raw);
    if (people !== null && (!Number.isInteger(people) || people < 1 || people > 500)) {
      this.peopleError.set('Enter a people count from 1 to 500.');
      return;
    }
    this.modal().nativeElement.close();
    this.applied.emit({
      shootTypes: this.draftShootTypes(),
      people,
      timesOfDay: this.draftPeriods(),
      setting: this.draftSetting(),
      tags: this.draftTags(),
    });
  }
  private toggled(values: string[], value: string): string[] {
    return values.includes(value) ? values.filter((other) => other !== value) : [...values, value];
  }
}
