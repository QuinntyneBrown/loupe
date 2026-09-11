import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  viewChild,
  inject,
  Injector,
  OnInit,
  signal,
  viewChildren,
} from '@angular/core';
import { REFERENCE_SERVICE, ReferenceSummary } from 'api';
import { ReferenceCard } from 'components';

@Component({
  selector: 'lp-reference-collection',
  imports: [ReferenceCard],
  templateUrl: './reference-collection.html',
  styleUrl: './reference-collection.css',
})
export class ReferenceCollection implements OnInit {
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroyed = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly cards = viewChildren(ReferenceCard);
  private readonly emptyHeading = viewChild<ElementRef<HTMLElement>>('emptyHeading');
  private readonly retryButton = viewChild<ElementRef<HTMLElement>>('retryButton');
  readonly items = signal<ReferenceSummary[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly loaded = signal(false);
  readonly nextCursor = signal<string | null>(null);
  readonly skeletonTiles = Array.from({ length: 8 }, (_, index) => index);
  ngOnInit(): void {
    void this.load();
  }
  async load(retry = false): Promise<void> {
    if (this.loading()) return;
    this.loading.set(true);
    this.failed.set(false);
    const previousCount = this.items().length;
    try {
      const page = await this.service.list(this.nextCursor() ?? undefined);
      if (this.destroyed.destroyed) return;
      this.items.update((items) => [
        ...items,
        ...page.items.filter((item) => !items.some((existing) => existing.id === item.id)),
      ]);
      this.nextCursor.set(page.nextCursor);
      this.loaded.set(true);
      if (retry || (previousCount && this.items().length > previousCount))
        afterNextRender(
          () => {
            if (this.destroyed.destroyed) return;
            const card = this.cards()[previousCount] ?? this.cards()[0];
            if (card) card.focus();
            else this.emptyHeading()?.nativeElement.focus();
          },
          { injector: this.injector },
        );
    } catch {
      if (!this.destroyed.destroyed) {
        this.failed.set(true);
        if (retry)
          afterNextRender(
            () => {
              if (!this.destroyed.destroyed) this.retryButton()?.nativeElement.focus();
            },
            { injector: this.injector },
          );
      }
    } finally {
      if (!this.destroyed.destroyed) this.loading.set(false);
    }
  }
}
