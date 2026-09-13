import { Component, computed, ElementRef, input, output, signal, viewChild } from '@angular/core';

@Component({
  selector: 'lp-video-card',
  templateUrl: './video-card.html',
  styleUrl: './video-card.css',
})
export class VideoCard {
  private readonly link = viewChild.required<ElementRef<HTMLAnchorElement>>('link');
  focus(): void {
    this.link().nativeElement.focus();
  }
  readonly title = input.required<string>();
  readonly url = input.required<string>();
  readonly thumbnailUrl = input.required<string>();
  readonly topicLabel = input.required<string>();
  readonly channel = input<string | null>(null);
  readonly summary = input<string | null>(null);
  readonly tags = input<string[]>([]);
  readonly indexed = input(true);
  readonly score = input<number | null>(null);
  readonly editRequested = output<void>();
  readonly imageFailed = signal(false);
  readonly match = computed(() => {
    const score = this.score();
    return score === null ? null : `${Math.round(score * 100)}% match`;
  });
}
