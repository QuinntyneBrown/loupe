import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  output,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ServiceError,
  VIDEO_SERVICE,
  VIDEO_TOPIC_LABELS,
  VIDEO_TOPICS,
  VideoResult,
  VideoSearchRequest,
  VideoTagFacet,
  VideoTopic,
} from 'api';
import { VideoCard } from 'components';

@Component({
  selector: 'lp-video-collection',
  imports: [VideoCard, FormsModule],
  templateUrl: './video-collection.html',
  styleUrl: './video-collection.css',
})
export class VideoCollection {
  readonly addRequested = output<void>();
  readonly editRequested = output<VideoResult>();
  readonly topics = VIDEO_TOPICS.map((topic) => ({
    value: topic,
    label: VIDEO_TOPIC_LABELS[topic],
  }));
  private readonly service = inject(VIDEO_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly cards = viewChildren(VideoCard);
  private readonly emptyHeading = viewChild<ElementRef<HTMLElement>>('emptyHeading');
  readonly items = signal<VideoResult[]>([]);
  readonly total = signal<number | null>(null);
  readonly cursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly query = signal('');
  readonly mode = signal<'keyword' | 'meaning'>('keyword');
  readonly topic = signal<VideoTopic | null>(null);
  readonly selectedTags = signal<string[]>([]);
  readonly facets = signal<VideoTagFacet[]>([]);
  readonly queryError = signal('');
  readonly meaningUnavailable = signal(false);
  readonly skeletons = [0, 1, 2, 3, 4, 5, 6, 7];
  /** The filter the current list was loaded with. */
  private readonly applied = signal<VideoSearchRequest>({});
  readonly filtered = computed(
    () => !!(this.query().trim() || this.topic() || this.selectedTags().length),
  );
  readonly searching = computed(() => {
    const applied = this.applied();
    return !!(applied.query || applied.topic || applied.tags?.length);
  });
  private generation = 0;
  private failedRefresh = false;
  constructor() {
    void this.load();
    void this.loadFacets();
  }
  topicLabel(item: VideoResult): string {
    return VIDEO_TOPIC_LABELS[item.topic] ?? item.topic;
  }
  tagNames(item: VideoResult): string[] {
    return item.tags.map((tag) => tag.name);
  }
  tagSelected(name: string): boolean {
    return this.selectedTags().some((tag) => tag.toUpperCase() === name.toUpperCase());
  }
  add(item: VideoResult): void {
    ++this.generation;
    this.loading.set(false);
    void this.loadFacets();
    if (
      this.total() === null ||
      this.searching() ||
      this.items().some((existing) => existing.id === item.id)
    ) {
      void this.refresh();
      return;
    }
    this.items.update((items) => [item, ...items]);
    this.total.update((total) => (total ?? 0) + 1);
  }
  replace(item: VideoResult): void {
    this.items.update((items) =>
      items.map((existing) => (existing.id === item.id ? item : existing)),
    );
    void this.loadFacets();
  }
  remove(id: string): void {
    void this.loadFacets();
    if (!this.items().some((existing) => existing.id === id)) return;
    this.items.update((items) => items.filter((existing) => existing.id !== id));
    this.total.update((total) => Math.max(0, (total ?? 1) - 1));
  }
  submit(): void {
    this.queryError.set('');
    this.meaningUnavailable.set(false);
    if (this.mode() === 'meaning' && !this.query().trim()) {
      this.queryError.set("Describe what you're looking for to search by meaning.");
      return;
    }
    void this.refresh();
  }
  chooseMode(mode: 'keyword' | 'meaning'): void {
    this.mode.set(mode);
    this.submit();
  }
  chooseTopic(topic: VideoTopic | null): void {
    this.topic.set(topic);
    this.submit();
  }
  toggleTag(name: string): void {
    this.selectedTags.update((tags) =>
      this.tagSelected(name)
        ? tags.filter((tag) => tag.toUpperCase() !== name.toUpperCase())
        : [...tags, name],
    );
    this.submit();
  }
  clearFilters(): void {
    this.query.set('');
    this.topic.set(null);
    this.selectedTags.set([]);
    this.submit();
  }
  useKeyword(): void {
    this.chooseMode('keyword');
  }
  refresh(): Promise<void> {
    ++this.generation;
    this.loading.set(false);
    return this.load(false, true);
  }
  retry(): Promise<void> {
    return this.load(true, this.failedRefresh);
  }
  private request(cursor?: string): VideoSearchRequest {
    const query = this.query().trim();
    return {
      ...(query ? { query } : {}),
      ...(this.topic() ? { topic: this.topic() } : {}),
      ...(this.selectedTags().length ? { tags: this.selectedTags() } : {}),
      mode: this.mode(),
      ...(cursor ? { cursor } : {}),
    };
  }
  async loadFacets(): Promise<void> {
    try {
      const facets = await this.service.tags();
      if (!this.destroy.destroyed) this.facets.set(facets);
    } catch {
      // The tag chips are a convenience; the list and the search box work without them.
    }
  }
  async load(restoreFocus = false, refresh = false): Promise<void> {
    if (this.loading()) return;
    const generation = ++this.generation;
    const previousCount = refresh ? 0 : this.items().length;
    const target = refresh ? Math.max(24, this.items().length) : 0;
    this.failedRefresh = refresh;
    this.loading.set(true);
    this.failed.set(false);
    const request = refresh
      ? this.request()
      : { ...this.applied(), cursor: this.cursor() ?? undefined };
    try {
      let result = await this.service.list(request);
      const incoming = [...result.items];
      while (refresh && incoming.length < target && result.nextCursor) {
        result = await this.service.list({ ...request, cursor: result.nextCursor });
        incoming.push(...result.items);
      }
      if (this.destroy.destroyed || generation !== this.generation) return;
      if (refresh) {
        this.items.set([]);
        this.applied.set({ ...request, cursor: undefined });
      }
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
    } catch (error) {
      if (this.destroy.destroyed || generation !== this.generation) return;
      const code = error instanceof ServiceError ? error.code : '';
      if (
        request.mode === 'meaning' &&
        ['integration_not_configured', 'service_unavailable'].includes(code)
      ) {
        this.items.set([]);
        this.total.set(0);
        this.cursor.set(null);
        this.applied.set({ ...request, cursor: undefined });
        this.meaningUnavailable.set(true);
      } else if (
        code === 'invalid_request' &&
        error instanceof ServiceError &&
        error.errors['query']?.length
      )
        this.queryError.set(error.errors['query'][0]);
      else this.failed.set(true);
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
}
