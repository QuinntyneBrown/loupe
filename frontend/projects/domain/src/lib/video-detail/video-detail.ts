import {
  afterNextRender,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ServiceError, VIDEO_SERVICE, VIDEO_TOPIC_LABELS, VideoResult } from 'api';
import { VideoPlayer } from 'components';

@Component({
  selector: 'lp-video-detail',
  imports: [VideoPlayer, RouterLink, DatePipe],
  templateUrl: './video-detail.html',
  styleUrl: './video-detail.css',
})
export class VideoDetail {
  readonly id = input.required<string>();
  readonly editRequested = output<VideoResult>();
  readonly deleteRequested = output<VideoResult>();
  readonly item = signal<VideoResult | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly unavailable = signal(false);
  private readonly service = inject(VIDEO_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly editButton = viewChild<ElementRef<HTMLButtonElement>>('editButton');
  private generation = 0;
  constructor() {
    effect(() => {
      const id = this.id();
      void this.load(id);
    });
  }
  topicLabel(item: VideoResult): string {
    return VIDEO_TOPIC_LABELS[item.topic] ?? item.topic;
  }
  retry(): Promise<void> {
    return this.load(this.id());
  }
  focusActions(): void {
    afterNextRender(
      () => {
        if (!this.destroy.destroyed) this.editButton()?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  private async load(id: string): Promise<void> {
    const generation = ++this.generation;
    this.loading.set(true);
    this.failed.set(false);
    this.unavailable.set(false);
    try {
      const result = await this.service.get(id);
      if (this.destroy.destroyed || generation !== this.generation) return;
      this.item.set(result);
    } catch (error) {
      if (this.destroy.destroyed || generation !== this.generation) return;
      this.item.set(null);
      if (error instanceof ServiceError && error.code === 'item_unavailable')
        this.unavailable.set(true);
      else this.failed.set(true);
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
}
