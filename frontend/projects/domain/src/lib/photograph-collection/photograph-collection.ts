import { Component, inject, OnInit, signal } from '@angular/core';
import { PHOTOGRAPH_SERVICE, PhotographSummary } from 'api';
import { PhotographCard } from 'components';

@Component({
  selector: 'lp-photograph-collection',
  imports: [PhotographCard],
  templateUrl: './photograph-collection.html',
  styleUrl: './photograph-collection.css',
})
export class PhotographCollection implements OnInit {
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  readonly items = signal<PhotographSummary[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly loaded = signal(false);
  readonly nextCursor = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    if (this.loading()) return;
    this.loading.set(true);
    this.failed.set(false);
    try {
      const page = await this.service.list(this.nextCursor() ?? undefined);
      this.items.update((items) => [...items, ...page.items]);
      this.nextCursor.set(page.nextCursor);
      this.loaded.set(true);
    } catch {
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
