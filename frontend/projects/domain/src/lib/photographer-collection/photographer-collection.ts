import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  signal,
  viewChild,
  viewChildren,
  output,
} from '@angular/core';
import { PHOTOGRAPHER_SERVICE, PhotographerSummary, PhotographerResult } from 'api';
import { PhotographerCard } from 'components';

@Component({
  selector: 'lp-photographer-collection',
  imports: [PhotographerCard],
  templateUrl: './photographer-collection.html',
  styleUrl: './photographer-collection.css',
})
export class PhotographerCollection {
  readonly addRequested = output<void>();
  private generation = 0;
  private failedRefresh = false;
  add(item: PhotographerResult): void {
    ++this.generation;
    this.loading.set(false);
    if (this.total() === null) {
      void this.refresh();
      return;
    }
    if (this.items().some((existing) => existing.id === item.id)) return;
    this.items.update((items) => [{ ...item, referenceCount: 0, references: [] }, ...items]);
    this.total.update((total) => (total ?? 0) + 1);
  }
  refresh(): Promise<void> {
    ++this.generation;
    this.loading.set(false);
    return this.load(false, true);
  }
  retry(): Promise<void> {
    return this.load(true, this.failedRefresh);
  }
  private readonly service = inject(PHOTOGRAPHER_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly cards = viewChildren(PhotographerCard);
  private readonly emptyHeading = viewChild<ElementRef<HTMLElement>>('emptyHeading');
  readonly items = signal<PhotographerSummary[]>([]);
  readonly total = signal<number | null>(null);
  readonly cursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly skeletons = [0, 1, 2, 3, 4, 5, 6, 7];
  constructor() {
    void this.load();
  }
  tagNames(item: PhotographerSummary): string[] {
    return item.tags.map((tag) => tag.name);
  }
  async load(restoreFocus = false, refresh = false): Promise<void> {
    if (this.loading()) return;
    const generation = ++this.generation;
    const previousCount = refresh ? 0 : this.items().length;
    const target = refresh ? Math.max(24, this.items().length) : 0;
    this.failedRefresh = refresh;
    this.loading.set(true);
    this.failed.set(false);
    try {
      let result = await this.service.list(refresh ? undefined : (this.cursor() ?? undefined));
      const incoming = [...result.items];
      while (refresh && incoming.length < target && result.nextCursor) {
        result = await this.service.list(result.nextCursor);
        incoming.push(...result.items);
      }
      if (this.destroy.destroyed || generation !== this.generation) return;
      if (refresh) this.items.set([]);
      const existing = new Set(this.items().map((item) => item.id));
      this.items.update((items) => [
        ...items,
        ...incoming.filter((item) => !existing.has(item.id)),
      ]);
      this.total.set(result.totalCount);
      this.cursor.set(result.nextCursor);
      if (restoreFocus)
        afterNextRender(
          () => {
            if (this.destroy.destroyed || generation !== this.generation) return;
            const card = this.cards()[Math.min(previousCount, this.items().length - 1)];
            if (card) card.focus();
            else this.emptyHeading()?.nativeElement.focus();
          },
          { injector: this.injector },
        );
    } catch {
      if (!this.destroy.destroyed && generation === this.generation) this.failed.set(true);
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
}
