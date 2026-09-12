import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LocationInput, LocationResult, LocationSetting } from 'api';

type Field =
  | 'name'
  | 'addressLine1'
  | 'addressLine2'
  | 'locality'
  | 'region'
  | 'postalCode'
  | 'country'
  | 'latitude'
  | 'longitude'
  | 'setting'
  | 'scoutingBrief'
  | 'notes'
  | 'tags';

const limits: Partial<Record<Field, number>> = {
  name: 200,
  addressLine1: 200,
  addressLine2: 200,
  locality: 100,
  region: 100,
  postalCode: 20,
  country: 100,
  scoutingBrief: 2000,
  notes: 10000,
};

@Component({
  selector: 'lp-location-form',
  imports: [FormsModule],
  templateUrl: './location-form.html',
  styleUrl: './location-form.css',
})
export class LocationForm {
  readonly initial = input<LocationResult | null>(null);
  readonly busy = input(false);
  readonly idPrefix = input('location');
  readonly mode = input<'full' | 'details'>('full');
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly root = viewChild.required<ElementRef<HTMLElement>>('root');
  readonly name = signal('');
  readonly addressLine1 = signal('');
  readonly addressLine2 = signal('');
  readonly locality = signal('');
  readonly region = signal('');
  readonly postalCode = signal('');
  readonly country = signal('');
  readonly latitude = signal('');
  readonly longitude = signal('');
  readonly setting = signal<LocationSetting | ''>('');
  readonly scoutingBrief = signal('');
  readonly notes = signal('');
  readonly tags = signal<string[]>([]);
  readonly tag = signal('');
  readonly errors = signal<Partial<Record<Field, string>>>({});
  readonly settings: LocationSetting[] = ['Indoor', 'Outdoor', 'Mixed'];
  readonly addressLines = [
    { key: 'addressLine1' as const, label: 'Address line 1', value: this.addressLine1 },
    { key: 'addressLine2' as const, label: 'Address line 2', value: this.addressLine2 },
  ];
  readonly rows = [
    [
      {
        key: 'locality' as const,
        label: 'Locality',
        value: this.locality,
        placeholder: 'Richmond',
        mono: false,
      },
      {
        key: 'region' as const,
        label: 'Region',
        value: this.region,
        placeholder: 'London',
        mono: false,
      },
    ],
    [
      {
        key: 'postalCode' as const,
        label: 'Postal code',
        value: this.postalCode,
        placeholder: '',
        mono: true,
      },
      {
        key: 'country' as const,
        label: 'Country',
        value: this.country,
        placeholder: 'United Kingdom',
        mono: false,
      },
    ],
    [
      {
        key: 'latitude' as const,
        label: 'Latitude',
        value: this.latitude,
        placeholder: '51.487213',
        mono: true,
      },
      {
        key: 'longitude' as const,
        label: 'Longitude',
        value: this.longitude,
        placeholder: '-0.287604',
        mono: true,
      },
    ],
  ];
  readonly value = computed<LocationInput>(() => ({
    name: this.name(),
    addressLine1: this.addressLine1().trim() || null,
    addressLine2: this.addressLine2().trim() || null,
    locality: this.locality().trim() || null,
    region: this.region().trim() || null,
    postalCode: this.postalCode().trim() || null,
    country: this.country().trim() || null,
    coordinates:
      this.latitude().trim() || this.longitude().trim()
        ? { latitude: this.latitude().trim(), longitude: this.longitude().trim() }
        : null,
    setting: this.setting() || null,
    scoutingBrief: this.scoutingBrief().trim() || null,
    notes: this.notes().trim() || null,
    tags: this.tags().map((name) => ({ name, category: null })),
  }));
  private readonly baseline = signal('');
  readonly dirty = computed(() => JSON.stringify(this.value()) !== this.baseline());
  constructor() {
    this.baseline.set(JSON.stringify(this.value()));
    afterNextRender(() => this.reset());
  }
  reset(): void {
    const initial = this.initial();
    this.name.set(initial?.name ?? '');
    this.addressLine1.set(initial?.addressLine1 ?? '');
    this.addressLine2.set(initial?.addressLine2 ?? '');
    this.locality.set(initial?.locality ?? '');
    this.region.set(initial?.region ?? '');
    this.postalCode.set(initial?.postalCode ?? '');
    this.country.set(initial?.country ?? '');
    this.latitude.set(initial?.coordinates?.latitude ?? '');
    this.longitude.set(initial?.coordinates?.longitude ?? '');
    this.setting.set(initial?.setting ?? '');
    this.scoutingBrief.set(initial?.scoutingBrief ?? '');
    this.notes.set(initial?.notes ?? '');
    this.tags.set(initial?.tags.map((tag) => tag.name) ?? []);
    this.tag.set('');
    this.errors.set({});
    this.baseline.set(JSON.stringify(this.value()));
  }
  addTag(event: Event): void {
    event.preventDefault();
    const name = this.tag().trim().normalize('NFC');
    if (!name) return;
    if (Array.from(name).length > 50 || this.tags().length >= 50) {
      this.fail({ tags: 'Use up to 50 tags, with 50 characters or fewer per tag.' });
      return;
    }
    if (this.tags().some((tag) => tag.toUpperCase() === name.toUpperCase())) {
      this.fail({ tags: 'This tag is already included.' });
      return;
    }
    this.tags.update((tags) => [...tags, name]);
    this.tag.set('');
    this.clear('tags');
  }
  removeTag(name: string): void {
    this.tags.update((tags) => tags.filter((tag) => tag !== name));
  }
  clear(field: Field): void {
    if (this.errors()[field]) this.errors.update(({ [field]: _, ...rest }) => rest);
  }
  validate(): boolean {
    const errors: Partial<Record<Field, string>> = {};
    if (!this.name().trim()) errors.name = 'Enter a name.';
    for (const [field, limit] of Object.entries(limits) as [Field, number][]) {
      const value = this[field as Exclude<Field, 'latitude' | 'longitude' | 'setting' | 'tags'>]();
      if (Array.from(value.trim()).length > limit)
        errors[field] = `Use ${limit.toLocaleString('en')} characters or fewer.`;
    }
    const latitude = this.latitude().trim();
    const longitude = this.longitude().trim();
    if (latitude && !longitude)
      errors.longitude = 'Enter a longitude as well, or clear the latitude.';
    else if (longitude && !latitude)
      errors.latitude = 'Enter a latitude as well, or clear the longitude.';
    else if (latitude && longitude) {
      if (!LocationForm.degrees(latitude, 90))
        errors.latitude = 'Enter decimal degrees from -90 to 90 with up to six decimal places.';
      if (!LocationForm.degrees(longitude, 180))
        errors.longitude = 'Enter decimal degrees from -180 to 180 with up to six decimal places.';
    }
    return this.fail(errors);
  }
  applyErrors(errors: Record<string, string[]>): boolean {
    const known: Partial<Record<Field, string>> = {};
    for (const [field, messages] of Object.entries(errors))
      if (field in limits || ['latitude', 'longitude', 'setting', 'tags'].includes(field))
        known[field as Field] = messages[0] ?? 'Check this field.';
    return !this.fail(known);
  }
  private fail(errors: Partial<Record<Field, string>>): boolean {
    this.errors.set(errors);
    const first = (Object.keys(errors) as Field[])[0];
    if (first)
      afterNextRender(
        // NgModel re-enables a control in a microtask, so focus after it.
        () =>
          queueMicrotask(() => {
            if (!this.destroy.destroyed)
              this.root()
                .nativeElement.querySelector<HTMLElement>(`[data-field="${first}"]`)
                ?.focus();
          }),
        { injector: this.injector },
      );
    return !first;
  }
  private static degrees(value: string, limit: number): boolean {
    if (!/^[-+]?\d+(\.\d{1,6})?$/.test(value)) return false;
    return Math.abs(Number(value)) <= limit;
  }
}
