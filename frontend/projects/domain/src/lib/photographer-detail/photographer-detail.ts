import {
  afterNextRender,
  DestroyRef,
  ElementRef,
  Injector,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  output,
  viewChild,
  viewChildren,
} from '@angular/core';
import { PhotographerNotes } from '../photographer-notes/photographer-notes';
import { PhotographerTags } from '../photographer-tags/photographer-tags';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  PHOTOGRAPHER_SERVICE,
  PhotographerResult,
  ReferenceSummary,
  REFERENCE_SERVICE,
  ServiceError,
} from 'api';

@Component({
  selector: 'lp-photographer-detail',
  imports: [DatePipe, RouterLink, PhotographerNotes, PhotographerTags],
  templateUrl: './photographer-detail.html',
  styleUrl: './photographer-detail.css',
})
export class PhotographerDetail {
  private readonly referenceService = inject(REFERENCE_SERVICE);
  readonly referenceBusy = signal(false);
  readonly linkError = signal('');
  readonly unlinked = signal<{
    reference: ReferenceSummary;
    revision: number;
    position: number;
  } | null>(null);
  readonly unlinkRetry = signal<ReferenceSummary | null>(null);
  private unlinkRevision: { id: string; revision: number } | null = null;
  private readonly referenceLinks = viewChildren<ElementRef<HTMLAnchorElement>>('referenceLink');
  private readonly linkedHeading = viewChild<ElementRef<HTMLElement>>('linkedHeading');
  private focusReference(position: number): void {
    afterNextRender(
      () => {
        if (this.destroy.destroyed) return;
        const links = this.referenceLinks();
        (
          links[Math.max(0, Math.min(position, links.length - 1))]?.nativeElement ??
          this.linkedHeading()?.nativeElement
        )?.focus();
      },
      { injector: this.injector },
    );
  }
  private finishUnlink(reference: ReferenceSummary, revision: number): void {
    const position = this.references().findIndex((item) => item.id === reference.id);
    this.unlinked.set({ reference, revision, position });
    this.removeReference(reference.id);
    this.unlinkRetry.set(null);
    this.unlinkRevision = null;
    this.focusReference(position);
  }
  async unlink(reference: ReferenceSummary): Promise<void> {
    if (this.referenceBusy()) return;
    this.referenceBusy.set(true);
    this.linkError.set('');
    this.unlinkRetry.set(reference);
    try {
      const current = await this.referenceService.get(reference.id);
      if (this.destroy.destroyed) return;
      if (current.photographer?.id !== this.id()) {
        if (
          !current.photographer &&
          this.unlinkRevision?.id === reference.id &&
          current.revision === this.unlinkRevision.revision + 1
        ) {
          this.finishUnlink(reference, current.revision);
          return;
        }
        this.linkError.set('This reference changed. It is no longer linked to this photographer.');
        this.removeReference(reference.id);
        this.unlinkRetry.set(null);
        return;
      }
      this.unlinkRevision = { id: reference.id, revision: current.revision };
      const result = await this.referenceService.setPhotographer(
        reference.id,
        current.revision,
        null,
      );
      if (this.destroy.destroyed) return;
      this.finishUnlink(reference, result.revision);
    } catch (error) {
      if (!this.destroy.destroyed)
        this.linkError.set(
          error instanceof ServiceError && error.code === 'revision_conflict'
            ? 'This reference changed. Retry to review its current link.'
            : "Couldn't unlink this reference. Your library is unchanged unless the request completed. Retry to check.",
        );
    } finally {
      if (!this.destroy.destroyed) this.referenceBusy.set(false);
    }
  }
  private removeReference(id: string): void {
    if (!this.references().some((item) => item.id === id)) return;
    this.references.update((items) => items.filter((item) => item.id !== id));
    this.referenceCount.update((count) => (count === null ? null : Math.max(0, count - 1)));
  }
  async undoUnlink(): Promise<void> {
    const undo = this.unlinked();
    if (!undo || this.referenceBusy()) return;
    this.referenceBusy.set(true);
    this.linkError.set('');
    try {
      const result = await this.referenceService.setPhotographer(
        undo.reference.id,
        undo.revision,
        this.id(),
      );
      if (this.destroy.destroyed) return;
      this.finishUndo(result, undo.position);
    } catch (error) {
      if (!this.destroy.destroyed) {
        const stale = error instanceof ServiceError && error.code === 'revision_conflict';
        if (stale) {
          try {
            const latest = await this.referenceService.get(undo.reference.id);
            if (this.destroy.destroyed) return;
            if (latest.photographer?.id === this.id()) {
              this.finishUndo(latest, undo.position);
              return;
            }
          } catch {
            if (this.destroy.destroyed) return;
            this.linkError.set("Couldn't check the restored link. Try Undo again.");
            return;
          }
        }
        this.linkError.set(
          stale
            ? 'This reference changed. Undo cannot overwrite the newer assignment.'
            : "Couldn't restore the link. Try Undo again.",
        );
        if (stale) this.unlinked.set(null);
      }
    } finally {
      if (!this.destroy.destroyed) this.referenceBusy.set(false);
    }
  }
  private finishUndo(reference: ReferenceSummary, position: number): void {
    const exists = this.references().some((item) => item.id === reference.id);
    this.references.update((items) => {
      const next = items.filter((item) => item.id !== reference.id);
      next.splice(Math.max(0, position), 0, reference);
      return next;
    });
    if (!exists) this.referenceCount.update((count) => (count === null ? null : count + 1));
    this.unlinked.set(null);
    this.focusReference(position);
  }
  private readonly actions = viewChild<ElementRef<HTMLElement>>('actions');
  private readonly injector = inject(Injector);
  private readonly destroy = inject(DestroyRef);
  focusActions(): void {
    afterNextRender(
      () => {
        if (!this.destroy.destroyed) this.actions()?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  private readonly notes = viewChild(PhotographerNotes);
  private readonly tags = viewChild(PhotographerTags);
  readonly dirty = computed(
    () => !!(this.notes()?.dirty() || this.notes()?.busy() || this.tags()?.dirty()),
  );
  readonly editRequested = output<PhotographerResult>();
  readonly linkRequested = output<PhotographerResult>();
  private readonly linkButton = viewChild<ElementRef<HTMLButtonElement>>('linkButton');
  focusLink(): void {
    afterNextRender(
      () => {
        if (!this.destroy.destroyed) this.linkButton()?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
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
  private referenceGeneration = 0;
  private refreshTarget = 0;
  refreshReferences(additional = 0): void {
    this.refreshTarget = Math.max(24, this.references().length + additional);
    this.referenceGeneration++;
    this.referenceLoading.set(false);
    void this.loadReferences();
  }
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
    const referenceGeneration = ++this.referenceGeneration;
    const refresh = this.refreshTarget > 0;
    this.referenceLoading.set(true);
    this.referenceFailed.set(false);
    try {
      let result = await this.service.references(
        this.id(),
        refresh ? undefined : (this.cursor() ?? undefined),
      );
      let incoming = [...result.items];
      while (refresh && incoming.length < this.refreshTarget && result.nextCursor) {
        result = await this.service.references(this.id(), result.nextCursor);
        incoming.push(...result.items);
      }
      if (generation !== this.generation || referenceGeneration !== this.referenceGeneration)
        return;
      this.references.update((items) => {
        if (refresh) items = [];
        const ids = new Set(items.map((item) => item.id));
        return [...items, ...incoming.filter((item) => !ids.has(item.id))];
      });
      this.refreshTarget = 0;
      this.cursor.set(result.nextCursor);
      this.referenceCount.set(result.totalCount);
    } catch {
      if (generation === this.generation && referenceGeneration === this.referenceGeneration)
        this.referenceFailed.set(true);
    } finally {
      if (generation === this.generation && referenceGeneration === this.referenceGeneration)
        this.referenceLoading.set(false);
    }
  }
}
