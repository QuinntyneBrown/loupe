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
import { LOCATION_SERVICE, LocationResult, ServiceError } from 'api';

@Component({
  selector: 'lp-search-index-status',
  templateUrl: './search-index-status.html',
  styleUrl: './search-index-status.css',
})
export class SearchIndexStatus {
  readonly location = input.required<LocationResult>();
  readonly updated = output<LocationResult>();
  readonly busy = signal(false);
  readonly error = signal('');
  readonly label = computed(() => {
    switch (this.location().indexStatus) {
      case 'processing-report':
        return { text: 'Processing report', modifier: 'lp-status--queued' };
      case 'updating':
        return { text: 'Updating search', modifier: 'lp-status--analyzing' };
      case 'failed':
        return { text: 'Search indexing failed', modifier: 'lp-status--failed' };
      default:
        return null;
    }
  });
  readonly pending = computed(() =>
    ['processing-report', 'updating'].includes(this.location().indexStatus),
  );
  private readonly service = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private retryKey: string | null = null;
  constructor() {
    effect(() => {
      const pending = this.pending();
      const id = this.location().id;
      untracked(() => {
        this.generation++;
        clearTimeout(this.timer);
        if (pending) this.timer = setTimeout(() => void this.poll(id), 2000);
      });
    });
    this.destroy.onDestroy(() => {
      this.generation++;
      clearTimeout(this.timer);
    });
  }
  private async poll(id: string): Promise<void> {
    const generation = ++this.generation;
    try {
      const latest = await this.service.get(id);
      if (this.destroy.destroyed || generation !== this.generation) return;
      if (
        latest.indexStatus !== this.location().indexStatus ||
        latest.revision !== this.location().revision
      )
        this.updated.emit(latest);
      else if (this.pending()) this.timer = setTimeout(() => void this.poll(id), 2000);
    } catch {
      if (!this.destroy.destroyed && generation === this.generation && this.pending())
        this.timer = setTimeout(() => void this.poll(id), 2000);
    }
  }
  async retry(): Promise<void> {
    const item = this.location();
    if (this.busy() || item.indexStatus !== 'failed' || !item.indexOperationId) return;
    this.busy.set(true);
    this.error.set('');
    this.retryKey ??= crypto.randomUUID();
    try {
      await this.service.retryIndex(item.indexOperationId, item.revision, this.retryKey);
      if (this.destroy.destroyed) return;
      this.retryKey = null;
      const latest = await this.service.get(item.id);
      if (this.destroy.destroyed) return;
      this.updated.emit(latest);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : 'request_failed';
      this.error.set(
        code === 'revision_conflict'
          ? 'This location changed. Reload to retry.'
          : code === 'retry_unavailable'
            ? 'This run can no longer be retried. Save a change to index again.'
            : 'The retry could not be requested. Try again.',
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
