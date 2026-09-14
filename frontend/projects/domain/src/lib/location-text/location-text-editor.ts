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
import { LOCATION_SERVICE, LocationResult, LocationTextField, ServiceError } from 'api';

@Component({
  selector: 'lp-location-text-editor',
  imports: [FormsModule],
  templateUrl: './location-text-editor.html',
  styleUrl: './location-text-editor.css',
})
export class LocationTextEditor {
  readonly location = input.required<LocationResult>();
  readonly field = input.required<LocationTextField>();
  readonly saved = output<LocationResult>();
  readonly label = computed(() =>
    this.field() === 'scoutingBrief' ? 'Scouting brief' : 'Your notes',
  );
  readonly draft = signal('');
  readonly baseline = signal<LocationResult | null>(null);
  readonly normalized = computed(() => this.draft().replace(/\r\n?/g, '\n').trim());
  readonly dirty = computed(
    () => !!this.baseline() && this.normalized() !== (this.baseline()?.[this.field()] ?? ''),
  );
  readonly busy = signal(false);
  readonly reviewing = signal(false);
  readonly failed = signal(false);
  readonly conflict = signal(false);
  readonly unavailable = signal(false);
  readonly latest = signal<LocationResult | null>(null);
  readonly error = signal('');
  readonly maximum = computed(() => (this.field() === 'scoutingBrief' ? 2000 : 10000));
  readonly invalid = computed(() => [...this.normalized()].length > this.maximum());
  readonly status = computed(() =>
    this.busy() ? 'Saving' : this.failed() ? "Couldn't save" : this.dirty() ? 'Unsaved' : 'Saved',
  );
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  constructor() {
    effect(() => {
      const location = this.location(),
        field = this.field();
      untracked(() => {
        if (location.id !== this.baseline()?.id || !this.dirty()) {
          this.baseline.set(location);
          this.draft.set(location[field] ?? '');
        } else this.baseline.set(location);
      });
    });
  }
  async review(): Promise<void> {
    if (this.reviewing()) return;
    this.reviewing.set(true);
    try {
      const latest = await this.service.get(this.location().id);
      if (this.destroy.destroyed) return;
      this.latest.set(latest);
      this.baseline.set(latest);
      this.conflict.set(false);
      this.failed.set(false);
      this.error.set('');
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.unavailable()
            ? 'This location is no longer available. Your text is still here.'
            : 'The latest value could not be loaded. Your text is still here. Try again.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.reviewing.set(false);
    }
  }
  async save(): Promise<void> {
    const base = this.baseline();
    if (
      !base ||
      !this.dirty() ||
      this.busy() ||
      this.reviewing() ||
      this.conflict() ||
      this.unavailable() ||
      this.invalid()
    )
      return;
    this.busy.set(true);
    this.failed.set(false);
    this.error.set('');
    try {
      const result = await this.service.updateText(
        base.id,
        base.revision,
        this.field(),
        this.normalized() || null,
      );
      if (!this.destroy.destroyed) {
        this.baseline.set(result);
        this.draft.set(result[this.field()] ?? '');
        this.latest.set(null);
        this.saved.emit(result);
      }
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.failed.set(true);
        this.conflict.set(error instanceof ServiceError && error.code === 'revision_conflict');
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.conflict()
            ? 'This location changed. Review the latest value before saving.'
            : this.unavailable()
              ? 'This location is no longer available. Your text is still here.'
              : 'Your change could not be saved. It is still here. Try again.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
