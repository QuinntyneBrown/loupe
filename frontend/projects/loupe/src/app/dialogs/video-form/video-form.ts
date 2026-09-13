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
import { FormsModule } from '@angular/forms';
import {
  parseYouTubeVideoId,
  ServiceError,
  VIDEO_SERVICE,
  VIDEO_TOPIC_LABELS,
  VIDEO_TOPICS,
  VideoInput,
  VideoResult,
  VideoTopic,
} from 'api';

@Component({
  selector: 'lp-video-form',
  imports: [FormsModule],
  templateUrl: './video-form.html',
  styleUrl: './video-form.css',
})
export class VideoForm {
  /** The video being edited, or null to add a new one. */
  readonly video = input<VideoResult | null>(null);
  readonly saved = output<VideoResult>();
  readonly closed = output<void>();
  readonly deleteRequested = output<VideoResult>();
  readonly topics = VIDEO_TOPICS.map((topic) => ({
    value: topic,
    label: VIDEO_TOPIC_LABELS[topic],
  }));
  private readonly service = inject(VIDEO_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  readonly base = signal<VideoResult | null>(null);
  readonly url = signal('');
  readonly title = signal('');
  readonly topic = signal<VideoTopic>('posing');
  readonly channel = signal('');
  readonly summary = signal('');
  readonly notes = signal('');
  readonly tags = signal<string[]>([]);
  readonly tag = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly stale = signal(false);
  readonly retryable = signal(false);
  readonly editing = computed(() => !!this.video());
  readonly dirty = computed(() => {
    if (this.busy()) return true;
    const base = this.base();
    if (!base)
      return !!(
        this.url().trim() ||
        this.title().trim() ||
        this.channel().trim() ||
        this.summary().trim() ||
        this.notes().trim() ||
        this.tags().length ||
        this.tag().trim()
      );
    return (
      this.url().trim() !== base.url ||
      this.title().trim() !== base.title ||
      this.topic() !== base.topic ||
      this.channel().trim() !== (base.channel ?? '') ||
      this.summary().trim() !== (base.summary ?? '') ||
      this.notes().trim() !== (base.notes ?? '') ||
      this.tags().join('\n') !== base.tags.map((tag) => tag.name).join('\n')
    );
  });
  constructor() {
    afterNextRender(() => {
      const item = this.video();
      if (item) this.reset(item);
      this.modal().nativeElement.showModal();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  private reset(item: VideoResult): void {
    this.base.set(item);
    this.url.set(item.url);
    this.title.set(item.title);
    this.topic.set(item.topic);
    this.channel.set(item.channel ?? '');
    this.summary.set(item.summary ?? '');
    this.notes.set(item.notes ?? '');
    this.tags.set(item.tags.map((tag) => tag.name));
  }
  addTag(event: Event): void {
    event.preventDefault();
    const name = this.tag().trim().normalize('NFC');
    if (!name) return;
    if (Array.from(name).length > 50 || this.tags().length >= 50) {
      this.error.set('Use up to 50 tags, with 50 characters or fewer per tag.');
      return;
    }
    if (this.tags().some((tag) => tag.toUpperCase() === name.toUpperCase())) {
      this.error.set('This tag is already included.');
      return;
    }
    this.tags.update((tags) => [...tags, name]);
    this.tag.set('');
    this.error.set('');
  }
  removeTag(name: string): void {
    this.tags.update((tags) => tags.filter((tag) => tag !== name));
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy()) this.closed.emit();
  }
  requestDelete(): void {
    const item = this.base();
    if (item && !this.busy()) this.deleteRequested.emit(item);
  }
  async review(): Promise<void> {
    const base = this.base();
    if (!base || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const latest = await this.service.get(base.id);
      if (this.destroy.destroyed) return;
      this.base.set(latest);
      this.stale.set(false);
      this.retryable.set(false);
      this.error.set('Latest video loaded. Review your edits, then save again.');
    } catch {
      if (!this.destroy.destroyed)
        this.error.set("Couldn't load the latest video. Your edits are kept.");
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  private validate(): VideoInput | null {
    const url = this.url().trim();
    const title = this.title().trim();
    if (!parseYouTubeVideoId(url) || Array.from(url).length > 2048) {
      this.error.set(
        'Enter a YouTube video URL such as https://www.youtube.com/watch?v=… or https://youtu.be/….',
      );
      return null;
    }
    if (!title || Array.from(title).length > 200) {
      this.error.set('Enter a title using 200 characters or fewer.');
      return null;
    }
    if (
      Array.from(this.channel().trim()).length > 200 ||
      Array.from(this.summary().trim()).length > 4000 ||
      Array.from(this.notes().trim()).length > 10000
    ) {
      this.error.set(
        'Use 200 characters or fewer for the channel, 4,000 for what it covers, and 10,000 for notes.',
      );
      return null;
    }
    if (this.tag().trim()) {
      this.error.set('Press Enter to add the last tag, or clear it before saving.');
      return null;
    }
    return {
      url,
      title,
      topic: this.topic(),
      channel: this.channel().trim() || null,
      summary: this.summary().trim() || null,
      notes: this.notes().trim() || null,
      tags: this.tags().map((name) => ({ name, category: null })),
    };
  }
  async save(): Promise<void> {
    if (this.busy() || this.stale()) return;
    this.retryable.set(false);
    this.error.set('');
    const input = this.validate();
    if (!input) return;
    const base = this.base();
    this.busy.set(true);
    try {
      const result = base
        ? await this.service.update(base.id, { ...input, revision: base.revision })
        : await this.service.save(input);
      if (!this.destroy.destroyed) this.saved.emit(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.stale.set(code === 'revision_conflict');
      this.retryable.set(
        !['revision_conflict', 'invalid_request', 'video_conflict', 'item_unavailable'].includes(
          code,
        ),
      );
      this.error.set(
        code === 'revision_conflict'
          ? 'This video changed. Review latest before saving your edits.'
          : code === 'video_conflict'
            ? 'This video is already saved. Open its existing card or enter a different URL.'
            : code === 'item_unavailable'
              ? 'This video is no longer available.'
              : code === 'invalid_request'
                ? 'Check the URL, title, topic, and field lengths. Your details are kept.'
                : "Couldn't save this video. Your details are kept. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
