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
} from '@angular/core';
import { PHOTOGRAPHER_SERVICE, PhotographerSummary } from 'api';
import { PhotographerCard } from 'components';

@Component({
  selector: 'lp-photographer-collection',
  imports: [PhotographerCard],
  templateUrl: './photographer-collection.html',
  styleUrl: './photographer-collection.css',
})
export class PhotographerCollection {
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
  async load(restoreFocus = false): Promise<void> {
    if (this.loading()) return;
    const previousCount = this.items().length;
    this.loading.set(true);
    this.failed.set(false);
    try {
      const result = await this.service.list(this.cursor() ?? undefined);
      if (this.destroy.destroyed) return;
      const existing = new Set(this.items().map((item) => item.id));
      this.items.update((items) => [
        ...items,
        ...result.items.filter((item) => !existing.has(item.id)),
      ]);
      this.total.set(result.totalCount);
      this.cursor.set(result.nextCursor);
      if (restoreFocus)
        afterNextRender(
          () => {
            if (this.destroy.destroyed) return;
            const card = this.cards()[Math.min(previousCount, this.items().length - 1)];
            if (card) card.focus();
            else this.emptyHeading()?.nativeElement.focus();
          },
          { injector: this.injector },
        );
    } catch {
      if (!this.destroy.destroyed) this.failed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.loading.set(false);
    }
  }
}
