import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  viewChild,
  inject,
  Injector,
  effect,
  input,
  output,
  untracked,
  signal,
  viewChildren,
} from '@angular/core';
import { REFERENCE_SERVICE, ReferenceSummary, ReferenceResult } from 'api';
import { ReferenceCard } from 'components';

@Component({
  selector: 'lp-reference-collection',
  imports: [ReferenceCard],
  templateUrl: './reference-collection.html',
  styleUrl: './reference-collection.css',
})
export class ReferenceCollection {
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
  readonly boardId = input<string | null>(null);
  readonly tags = input<string[]>([]);
  readonly clearTags = output<void>();
  readonly saveRequested = output<void>();
  readonly busy = input(false);
  readonly boardsRequested = output<ReferenceSummary>();
  readonly removeRequested = output<ReferenceSummary>();
  readonly libraryCount = output<number>();
  private generation = 0;
  remove(id: string): void {
    const index = this.items().findIndex((item) => item.id === id);
    if (index < 0) return;
    this.items.update((items) => items.filter((item) => item.id !== id));
    this.focusItem(Math.min(index, this.items().length - 1));
  }
  restore(reference: ReferenceResult): void {
    if (this.boardId() && !reference.boardIds.includes(this.boardId()!)) return;
    const names = reference.tags.map((tag) => tag.name.normalize('NFC').toUpperCase());
    if (!this.tags().every((tag) => names.includes(tag.normalize('NFC').toUpperCase()))) return;
    this.items.update((items) =>
      [...items.filter((item) => item.id !== reference.id), reference].sort(
        (a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt) || a.id.localeCompare(b.id),
      ),
    );
    this.focusItem(this.items().findIndex((item) => item.id === reference.id));
  }
  private focusItem(index: number): void {
    const generation = this.generation;
    afterNextRender(
      () => {
        if (this.destroyed.destroyed || generation !== this.generation) return;
        const card = this.cards()[index];
        if (card) card.focus();
        else this.emptyHeading()?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  constructor() {
    effect(() => {
      this.boardId();
      this.tags();
      untracked(() => this.refresh());
    });
  }
  refresh(): void {
    this.generation++;
    this.items.set([]);
    this.nextCursor.set(null);
    this.loaded.set(false);
    this.loading.set(false);
    void this.load();
  }
  async load(retry = false): Promise<void> {
    if (this.loading()) return;
    this.loading.set(true);
    this.failed.set(false);
    const previousCount = this.items().length;
    const generation = this.generation;
    try {
      const page = await this.service.list(
        this.nextCursor() ?? undefined,
        this.boardId() ?? undefined,
        this.tags(),
      );
      if (this.destroyed.destroyed || generation !== this.generation) return;
      this.items.update((items) => [
        ...items,
        ...page.items.filter((item) => !items.some((existing) => existing.id === item.id)),
      ]);
      this.nextCursor.set(page.nextCursor);
      this.loaded.set(true);
      this.libraryCount.emit(page.libraryCount);
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
      if (!this.destroyed.destroyed && generation === this.generation) {
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
      if (!this.destroyed.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
}
