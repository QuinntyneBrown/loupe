import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  PHOTOGRAPHER_SERVICE,
  REFERENCE_SERVICE,
  PhotographerResult,
  ReferenceCandidate,
  ServiceError,
} from 'api';
@Component({
  selector: 'lp-link-photographer-references',
  imports: [DatePipe, FormsModule],
  templateUrl: './link-photographer-references.html',
  styleUrl: './link-photographer-references.css',
})
export class LinkPhotographerReferences {
  readonly photographer = input.required<PhotographerResult>();
  readonly closed = output<number>();
  readonly query = signal('');
  readonly items = signal<ReferenceCandidate[]>([]);
  readonly cursor = signal<string | null>(null);
  readonly total = signal<number | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly selected = signal(new Map<string, ReferenceCandidate>());
  readonly stale = signal<ReferenceCandidate | null>(null);
  readonly linked = signal(0);
  readonly dirty = computed(() => this.selected().size > 0 || this.saving());
  private readonly photographers = inject(PHOTOGRAPHER_SERVICE);
  private readonly references = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  constructor() {
    afterNextRender(() => {
      this.modal().nativeElement.showModal();
      void this.load();
    });
    this.destroy.onDestroy(() => {
      this.generation++;
      clearTimeout(this.timer);
      this.modal().nativeElement.close();
    });
  }
  search(value: string): void {
    this.query.set(value);
    clearTimeout(this.timer);
    this.generation++;
    this.items.set([]);
    this.cursor.set(null);
    this.total.set(null);
    this.loading.set(false);
    this.timer = setTimeout(() => void this.load(), 200);
  }
  async load(): Promise<void> {
    if (this.loading()) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.loadFailed.set(false);
    try {
      const result = await this.photographers.candidates(
        this.photographer().id,
        this.query().trim(),
        this.cursor() ?? undefined,
      );
      if (this.destroy.destroyed || generation !== this.generation) return;
      this.items.update((items) => {
        const ids = new Set(items.map((item) => item.id));
        return [...items, ...result.items.filter((item) => !ids.has(item.id))];
      });
      this.cursor.set(result.nextCursor);
      this.total.set(result.totalCount);
    } catch {
      if (!this.destroy.destroyed && generation === this.generation) this.loadFailed.set(true);
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
  choose(item: ReferenceCandidate, checked: boolean): void {
    if (this.saving() || this.stale()) return;
    this.selected.update((selected) => {
      const next = new Map(selected);
      if (checked) next.set(item.id, item);
      else next.delete(item.id);
      return next;
    });
    this.error.set('');
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.saving()) this.closed.emit(this.linked());
  }
  async review(): Promise<void> {
    const stale = this.stale();
    if (!stale || this.saving()) return;
    this.saving.set(true);
    try {
      const latest = await this.references.get(stale.id);
      if (this.destroy.destroyed) return;
      this.selected.update((selected) => new Map(selected).set(latest.id, latest));
      this.items.update((items) => items.map((item) => (item.id === latest.id ? latest : item)));
      this.stale.set(null);
      this.error.set(
        `Latest details loaded for ${latest.title}. Review the current photographer before linking again.`,
      );
    } catch {
      if (!this.destroy.destroyed)
        this.error.set("Couldn't load the latest reference. Cancel to review it in your library.");
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
  async save(): Promise<void> {
    if (this.saving() || this.stale() || !this.selected().size) return;
    this.saving.set(true);
    this.error.set('');
    try {
      for (const candidate of this.selected().values()) {
        try {
          await this.references.setPhotographer(
            candidate.id,
            candidate.revision,
            this.photographer().id,
          );
          if (this.destroy.destroyed) return;
          this.linked.update((count) => count + 1);
          this.selected.update((selected) => {
            const next = new Map(selected);
            next.delete(candidate.id);
            return next;
          });
        } catch (error) {
          if (this.destroy.destroyed) return;
          if (error instanceof ServiceError && error.code === 'revision_conflict')
            this.stale.set(candidate);
          this.error.set(
            `${this.linked()} linked. Couldn't link ${candidate.title}. ${this.stale() ? 'Review its latest details before trying again.' : 'Your remaining selections are kept; try linking again.'}`,
          );
          return;
        }
      }
      this.closed.emit(this.linked());
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
