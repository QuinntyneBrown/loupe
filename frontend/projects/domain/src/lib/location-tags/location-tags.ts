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
import { FormsModule } from '@angular/forms';
import { LOCATION_SERVICE, LocationResult, LocationTag, ServiceError } from 'api';

@Component({
  selector: 'lp-location-tags',
  imports: [FormsModule],
  templateUrl: './location-tags.html',
  styleUrl: './location-tags.css',
})
export class LocationTags {
  readonly location = input.required<LocationResult>();
  readonly saved = output<LocationResult>();
  readonly tags = signal<LocationTag[]>([]);
  readonly draft = signal('');
  readonly baseline = signal<LocationResult | null>(null);
  readonly busy = signal(false);
  readonly failed = signal(false);
  readonly conflict = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  readonly dirty = computed(() => {
    const base = this.baseline();
    return !!base && LocationTags.key(this.tags()) !== LocationTags.key(base.tags);
  });
  readonly status = computed(() =>
    this.busy() ? 'Saving' : this.failed() ? "Couldn't save" : this.dirty() ? 'Unsaved' : 'Saved',
  );
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  constructor() {
    effect(() => {
      const location = this.location();
      untracked(() => {
        if (location.id !== this.baseline()?.id || !this.dirty()) this.tags.set([...location.tags]);
        this.baseline.set(location);
      });
    });
  }
  private static key(tags: LocationTag[]): string {
    return tags.map((tag) => `${tag.name.toUpperCase()}=${tag.category ?? ''}`).join('\n');
  }
  add(event: Event): void {
    event.preventDefault();
    const name = this.draft().trim().normalize('NFC');
    if (!name) return;
    if ([...name].length > 50 || this.tags().length >= 50) {
      this.error.set('Use up to 50 tags, with 50 characters or fewer per tag.');
      return;
    }
    if (this.tags().some((tag) => tag.name.toUpperCase() === name.toUpperCase())) {
      this.error.set('This tag is already included.');
      return;
    }
    this.tags.update((tags) => [...tags, { name, category: null }]);
    this.draft.set('');
    this.error.set('');
    this.failed.set(false);
  }
  remove(name: string): void {
    this.tags.update((tags) => tags.filter((tag) => tag.name !== name));
    this.error.set('');
    this.failed.set(false);
  }
  async reload(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      const latest = await this.service.get(this.location().id);
      if (this.destroy.destroyed) return;
      this.baseline.set(latest);
      this.tags.set([...latest.tags]);
      this.conflict.set(false);
      this.failed.set(false);
      this.error.set('');
      this.saved.emit(latest);
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.unavailable()
            ? 'This location is no longer available.'
            : 'The latest tags could not be loaded. Try again.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async save(): Promise<void> {
    const base = this.baseline();
    if (!base || !this.dirty() || this.busy() || this.conflict() || this.unavailable()) return;
    this.busy.set(true);
    this.failed.set(false);
    this.error.set('');
    try {
      const result = await this.service.setTags(
        base.id,
        base.revision,
        this.tags().map(({ name, category }) => ({ name, category })),
      );
      if (!this.destroy.destroyed) {
        this.baseline.set(result);
        this.tags.set([...result.tags]);
        this.saved.emit(result);
      }
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.failed.set(true);
        this.conflict.set(error instanceof ServiceError && error.code === 'revision_conflict');
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.conflict()
            ? 'This location changed. Load the latest tags before saving.'
            : this.unavailable()
              ? 'This location is no longer available. Your tags are still here.'
              : 'Your tags could not be saved. They are still here. Try again.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
