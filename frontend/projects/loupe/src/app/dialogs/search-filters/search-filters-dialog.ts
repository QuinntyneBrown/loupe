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
import { BoardResult, SearchTagFacet } from 'api';
import { isSearchTag } from '../../pages/search/search-filter-validation';

@Component({
  selector: 'lp-search-filters-dialog',
  templateUrl: './search-filters-dialog.html',
  styleUrl: './search-filters-dialog.css',
})
export class SearchFiltersDialog {
  readonly tags = input.required<SearchTagFacet[] | null>();
  readonly boards = input.required<BoardResult[] | null>();
  readonly selectedTags = input.required<string[]>();
  readonly selectedBoards = input.required<string[]>();
  readonly tagsFailed = input(false);
  readonly boardsFailed = input(false);
  readonly retryTags = output<void>();
  readonly retryBoards = output<void>();
  readonly applied = output<{ tags: string[]; boardIds: string[] }>();
  readonly closed = output<void>();
  readonly draftTags = signal<string[]>([]);
  readonly draftBoards = signal<string[]>([]);
  readonly tagError = signal('');
  readonly boardError = signal('');
  readonly tagChoices = computed(() =>
    [...(this.tags() ?? []).map((tag) => tag.name), ...this.selectedTags()].filter(
      (tag, index, all) => all.findIndex((other) => this.key(other) === this.key(tag)) === index,
    ),
  );
  readonly boardChoices = computed(() => [
    ...(this.boards() ?? []),
    ...this.selectedBoards()
      .filter((id) => !this.boards()?.some((board) => board.id === id))
      .map((id) => ({
        id,
        referenceCount: null,
        name: this.boardsFailed()
          ? 'Board name unavailable'
          : this.boards()
            ? 'Unavailable board'
            : 'Loading board…',
      })),
  ]);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private readonly destroy = inject(DestroyRef);
  constructor() {
    afterNextRender(() => {
      this.draftTags.set([...this.selectedTags()]);
      this.draftBoards.set([...this.selectedBoards()]);
      this.modal().nativeElement.showModal();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  private key(value: string): string {
    const facet = this.tags()?.find(
      (tag) => tag.name === value || tag.selectedNames.includes(value),
    );
    return facet ? `known:${facet.normalizedName}` : `unknown:${value}`;
  }
  hasTag(name: string): boolean {
    return this.draftTags().some((tag) => this.key(tag) === this.key(name));
  }
  tagLabel(name: string): string {
    return isSearchTag(name) ? name : `Invalid tag: ${JSON.stringify(name)}`;
  }
  toggleTag(name: string): void {
    this.tagError.set('');
    if (!this.hasTag(name) && this.draftTags().length >= 10) {
      this.tagError.set('Choose up to 10 tags.');
      return;
    }
    this.draftTags.update((tags) =>
      this.hasTag(name) ? tags.filter((tag) => this.key(tag) !== this.key(name)) : [...tags, name],
    );
  }
  toggleBoard(id: string): void {
    this.boardError.set('');
    if (!this.draftBoards().includes(id) && this.draftBoards().length >= 10) {
      this.boardError.set('Choose up to 10 boards.');
      return;
    }
    this.draftBoards.update((ids) =>
      ids.includes(id) ? ids.filter((value) => value !== id) : [...ids, id],
    );
  }
  clear(): void {
    this.draftTags.set([]);
    this.draftBoards.set([]);
    this.tagError.set('');
    this.boardError.set('');
  }
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const controls =
      this.modal().nativeElement.querySelectorAll<HTMLButtonElement>('button:not([disabled])');
    const first = controls[0],
      last = controls[controls.length - 1];
    if (event.shiftKey ? event.target === first : event.target === last) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  apply(): void {
    this.modal().nativeElement.close();
    this.applied.emit({ tags: this.draftTags(), boardIds: this.draftBoards() });
  }
}
