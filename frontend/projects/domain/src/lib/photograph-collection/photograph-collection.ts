import {
  afterNextRender,
  Component,
  inject,
  Injector,
  input,
  OnInit,
  output,
  signal,
  viewChildren,
} from '@angular/core';
import { PHOTOGRAPH_SERVICE, PhotographSummary } from 'api';
import { PhotographCard } from 'components';

@Component({
  selector: 'lp-photograph-collection',
  imports: [PhotographCard],
  templateUrl: './photograph-collection.html',
  styleUrl: './photograph-collection.css',
})
export class PhotographCollection implements OnInit {
  readonly uploadRequested = output<void>();
  readonly detailBase = input.required<string>();
  private readonly cards = viewChildren(PhotographCard);
  private readonly injector = inject(Injector);
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  readonly items = signal<PhotographSummary[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly loaded = signal(false);
  readonly nextCursor = signal<string | null>(null);
  readonly skeletonTiles = Array.from({ length: 8 }, (_, index) => index);
  private loadVersion = 0;
  private refreshPending = false;
  refresh(): Promise<void> {
    this.refreshPending = true;
    return this.load(true);
  }

  ngOnInit(): void {
    void this.load();
  }
  critiqueLabel(item: PhotographSummary): string {
    if (!item.critiqueStatus || item.critiqueStatus === 'Succeeded')
      return item.hasCritique ? 'Critique ready' : 'No critique yet';
    const label = {
      Queued: 'Critique queued',
      Running: 'Critique running',
      Failed: 'Critique failed',
      Canceled: 'Critique canceled',
    }[item.critiqueStatus];
    return label + (item.hasCritique ? ' · Previous critique available' : '');
  }
  statusKindFor(item: PhotographSummary): 'queued' | 'analyzing' | 'ready' | 'failed' | null {
    if (!item.critiqueStatus) return null;
    return {
      Queued: 'queued' as const,
      Running: 'analyzing' as const,
      Succeeded: 'ready' as const,
      Failed: 'failed' as const,
      Canceled: 'failed' as const,
    }[item.critiqueStatus];
  }

  async load(refresh = this.refreshPending): Promise<void> {
    if (this.loading() && !refresh) return;
    const version = ++this.loadVersion;
    this.loading.set(true);
    this.failed.set(false);
    const previousCount = this.items().length;
    try {
      const page = await this.service.list(refresh ? undefined : (this.nextCursor() ?? undefined));
      if (version !== this.loadVersion) return;
      this.items.update((items) =>
        refresh
          ? page.items
          : [
              ...items,
              ...page.items.filter((item) => !items.some((existing) => existing.id === item.id)),
            ],
      );
      this.refreshPending = false;
      this.nextCursor.set(page.nextCursor);
      this.loaded.set(true);
      if (!refresh && previousCount > 0 && page.items.length)
        afterNextRender(() => this.cards()[previousCount]?.focus(), { injector: this.injector });
    } catch {
      if (version !== this.loadVersion) return;
      this.failed.set(true);
    } finally {
      if (version === this.loadVersion) this.loading.set(false);
    }
  }
}
