import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  BOARD_SERVICE,
  BoardResult,
  SEARCH_SERVICE,
  SearchRequest,
  SearchTagFacet,
  ServiceError,
} from 'api';
import { SearchResults } from 'domain';
import { SearchFiltersDialog } from '../../dialogs/search-filters/search-filters-dialog';
import { isSearchBoardId, isSearchTag, isSearchType } from './search-filter-validation';

@Component({
  selector: 'lp-search-page',
  imports: [FormsModule, SearchResults, SearchFiltersDialog],
  templateUrl: './search-page.html',
  styleUrl: './search-page.css',
})
export class SearchPage {
  readonly query = signal('');
  readonly type = signal('all');
  readonly mode = signal<string | null>(null);
  readonly started = signal(false);
  readonly fieldErrors = signal<Record<string, string>>({});
  readonly error = computed(() => this.fieldErrors()['query'] ?? '');
  readonly request = signal<SearchRequest | null>(null);
  readonly selectedTags = signal<string[]>([]);
  readonly selectedBoards = signal<string[]>([]);
  readonly tags = signal<SearchTagFacet[] | null>(null);
  readonly boards = signal<BoardResult[] | null>(null);
  readonly dialogOpen = signal(false);
  readonly tagsFailed = signal(false);
  readonly boardsFailed = signal(false);
  private readonly searchService = inject(SEARCH_SERVICE);
  private readonly boardService = inject(BOARD_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private tagsGeneration = 0;
  private boardsGeneration = 0;
  private readonly repeated = signal<Record<string, string[]>>({});
  readonly examples = [
    'Moody portraits with soft window light',
    'Minimalist architecture with strong shadows',
    'Photographers who use cinematic colour and negative space',
  ];
  readonly types = ['all', 'references', 'photographers'] as const;
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly results = viewChild.required(SearchResults);
  private readonly queryInput = viewChild.required<ElementRef<HTMLInputElement>>('queryInput');
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.repeated.set(
        Object.fromEntries(
          ['q', 'type', 'mode']
            .filter((field) => params.getAll(field).length > 1)
            .map((field) => [field, params.getAll(field)]),
        ),
      );
      const query = params.get('q') ?? '',
        type = params.get('type') ?? 'all';
      this.query.set(query);
      this.type.set(type);
      this.mode.set(params.get('mode'));
      this.selectedTags.set(params.getAll('tags'));
      this.selectedBoards.set(params.getAll('boardIds').map((id) => id.toLowerCase()));
      if (this.selectedBoards().length && this.boards() === null) void this.loadBoards();
      this.started.set(
        params.has('q') || type !== 'all' || params.has('tags') || params.has('boardIds'),
      );
      const request = this.validatedRequest();
      this.request.set(this.started() ? request : null);
    });
  }
  private validatedRequest(): SearchRequest | null {
    const query = this.query().normalize('NFC').trim(),
      type = this.type(),
      mode = this.mode();
    const errors: Record<string, string> = {};
    if (Array.from(query.normalize('NFC').trim()).length > 500 || query.includes('\0')) {
      errors['query'] = 'Use 500 characters or fewer without null characters.';
    }
    if (!isSearchType(type)) errors['type'] = 'Choose All, References or Photographers.';
    if (mode !== null && mode !== 'keyword') errors['mode'] = 'Only keyword search is supported.';
    if (this.selectedTags().length > 10) errors['tags'] = 'Choose up to 10 tags.';
    else if (!this.selectedTags().every(isSearchTag))
      errors['tags'] = 'Use tag names of 1–50 characters without null characters.';
    if (this.selectedBoards().length > 10) errors['boardIds'] = 'Choose up to 10 boards.';
    else if (!this.selectedBoards().every(isSearchBoardId))
      errors['boardIds'] = 'Choose valid board IDs.';
    if (this.repeated()['q']) errors['query'] = 'Use one query value.';
    if (this.repeated()['type']) errors['type'] = 'Choose one type.';
    if (this.repeated()['mode']) errors['mode'] = 'Choose one search mode.';
    this.fieldErrors.set(errors);
    if (Object.keys(errors).length || !isSearchType(type)) return null;
    return {
      query,
      type,
      tags: this.selectedTags().map((tag) => tag.normalize('NFC').trim()),
      boardIds: this.selectedBoards(),
      ...(mode === 'keyword' ? { mode } : {}),
    };
  }
  async submit(correction: 'q' | 'type' | 'mode' | null = 'q'): Promise<void> {
    if (correction)
      this.repeated.update((fields) => {
        const remaining = { ...fields };
        delete remaining[correction];
        return remaining;
      });
    const request = this.validatedRequest();
    const query = this.query().normalize('NFC').trim();
    if (request && JSON.stringify(this.request()) === JSON.stringify(request)) {
      await this.results().load(true);
      return;
    }
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q: this.repeated()['q'] ?? query,
        type: this.repeated()['type'] ?? (this.type() === 'all' ? null : this.type()),
        tags: this.selectedTags(),
        boardIds: this.selectedBoards(),
        mode: this.repeated()['mode'] ?? this.mode(),
      },
    });
  }
  chooseType(type: SearchRequest['type']): void {
    this.type.set(type);
    void this.submit('type');
  }
  focusQuery(): void {
    this.queryInput().nativeElement.focus();
  }
  useExample(query: string): void {
    this.query.set(query);
    void this.submit();
  }
  useKeyword(): void {
    this.mode.set('keyword');
    void this.submit('mode');
  }
  showValidation(error: ServiceError): void {
    this.fieldErrors.set(
      Object.fromEntries(
        Object.entries(error.errors).map(([field, messages]) => [field, messages.join(' ')]),
      ),
    );
  }
  openFilters(): void {
    this.dialogOpen.set(true);
    void this.loadTags();
    void this.loadBoards();
  }
  async loadTags(): Promise<void> {
    const generation = ++this.tagsGeneration;
    this.tags.set(null);
    this.tagsFailed.set(false);
    try {
      const selected = this.selectedTags().filter(isSearchTag);
      const choices = new Map<string, SearchTagFacet>();
      for (let start = 0; start < Math.max(selected.length, 1); start += 10) {
        const tags = await this.searchService.tags(selected.slice(start, start + 10));
        if (this.destroy.destroyed || generation !== this.tagsGeneration) return;
        for (const tag of tags) {
          const previous = choices.get(tag.normalizedName);
          choices.set(tag.normalizedName, {
            ...tag,
            selectedNames: [...(previous?.selectedNames ?? []), ...tag.selectedNames],
          });
        }
      }
      this.tags.set([...choices.values()]);
    } catch {
      if (!this.destroy.destroyed && generation === this.tagsGeneration) this.tagsFailed.set(true);
    }
  }
  async loadBoards(): Promise<void> {
    const generation = ++this.boardsGeneration;
    this.boards.set(null);
    this.boardsFailed.set(false);
    try {
      const boards = await this.boardService.list();
      if (!this.destroy.destroyed && generation === this.boardsGeneration) this.boards.set(boards);
    } catch {
      if (!this.destroy.destroyed && generation === this.boardsGeneration)
        this.boardsFailed.set(true);
    }
  }
  applyFilters(filters: { tags: string[]; boardIds: string[] }): void {
    this.selectedTags.set(filters.tags);
    this.selectedBoards.set(filters.boardIds);
    this.dialogOpen.set(false);
    void this.submit(null);
  }
  boardName(id: string): string {
    if (!isSearchBoardId(id)) return 'Invalid board ID';
    if (this.boardsFailed()) return 'Board name unavailable';
    if (this.boards() === null) return 'Loading board…';
    return this.boards()?.find((board) => board.id === id)?.name ?? 'Unavailable board';
  }
  removeTag(tag: string): void {
    this.selectedTags.update((tags) => tags.filter((value) => value !== tag));
    void this.submit(null);
  }
  removeBoard(id: string): void {
    this.selectedBoards.update((ids) => ids.filter((value) => value !== id));
    void this.submit(null);
  }
  clearFilters(): void {
    this.selectedTags.set([]);
    this.selectedBoards.set([]);
    void this.submit(null);
  }
}
