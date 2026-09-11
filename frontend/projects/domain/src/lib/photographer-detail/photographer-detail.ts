import {
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  output,
  viewChild,
} from '@angular/core';
import { PhotographerNotes } from '../photographer-notes/photographer-notes';
import { PhotographerTags } from '../photographer-tags/photographer-tags';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PHOTOGRAPHER_SERVICE, PhotographerResult, ReferenceSummary } from 'api';

@Component({
  selector: 'lp-photographer-detail',
  imports: [DatePipe, RouterLink, PhotographerNotes, PhotographerTags],
  templateUrl: './photographer-detail.html',
  styleUrl: './photographer-detail.css',
})
export class PhotographerDetail {
  private readonly notes = viewChild(PhotographerNotes);
  private readonly tags = viewChild(PhotographerTags);
  readonly dirty = computed(
    () => !!(this.notes()?.dirty() || this.notes()?.busy() || this.tags()?.dirty()),
  );
  readonly editRequested = output<PhotographerResult>();
  readonly deleteRequested = output<{ item: PhotographerResult; count: number }>();
  delete(menu: HTMLDetailsElement): void {
    menu.open = false;
    const item = this.item();
    const count = this.referenceCount();
    if (item && count !== null) this.deleteRequested.emit({ item, count });
  }
  edit(menu: HTMLDetailsElement): void {
    menu.open = false;
    const item = this.item();
    if (item) this.editRequested.emit(item);
  }
  readonly id = input.required<string>();
  private readonly service = inject(PHOTOGRAPHER_SERVICE);
  readonly item = signal<PhotographerResult | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly references = signal<ReferenceSummary[]>([]);
  readonly referenceCount = signal<number | null>(null);
  readonly referenceLoading = signal(false);
  readonly referenceFailed = signal(false);
  readonly cursor = signal<string | null>(null);
  readonly skeletons = [0, 1, 2, 3];
  readonly host = computed(() => {
    try {
      return new URL(this.item()?.portfolioUrl ?? '').hostname;
    } catch {
      return '';
    }
  });
  private generation = 0;
  constructor() {
    effect(() => {
      const id = this.id();
      void this.load(id);
    });
  }
  retry(): void {
    void this.load(this.id());
  }
  private async load(id: string): Promise<void> {
    const generation = ++this.generation;
    this.loading.set(true);
    this.failed.set(false);
    this.item.set(null);
    this.references.set([]);
    this.referenceCount.set(null);
    this.cursor.set(null);
    this.referenceLoading.set(false);
    this.referenceFailed.set(false);
    try {
      const result = await this.service.get(id);
      if (generation !== this.generation) return;
      this.item.set(result);
      void this.loadReferences();
    } catch {
      if (generation === this.generation) this.failed.set(true);
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }
  async loadReferences(): Promise<void> {
    if (this.referenceLoading()) return;
    const generation = this.generation;
    this.referenceLoading.set(true);
    this.referenceFailed.set(false);
    try {
      const result = await this.service.references(this.id(), this.cursor() ?? undefined);
      if (generation !== this.generation) return;
      this.references.update((items) => {
        const ids = new Set(items.map((item) => item.id));
        return [...items, ...result.items.filter((item) => !ids.has(item.id))];
      });
      this.cursor.set(result.nextCursor);
      this.referenceCount.set(result.totalCount);
    } catch {
      if (generation === this.generation) this.referenceFailed.set(true);
    } finally {
      if (generation === this.generation) this.referenceLoading.set(false);
    }
  }
}
