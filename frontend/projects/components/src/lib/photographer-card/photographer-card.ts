import { Component, computed, ElementRef, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'lp-photographer-card',
  imports: [RouterLink],
  templateUrl: './photographer-card.html',
  styleUrl: './photographer-card.css',
})
export class PhotographerCard {
  readonly searchResult = input(false);
  private readonly link = viewChild.required<ElementRef<HTMLAnchorElement>>('link');
  focus(): void {
    this.link().nativeElement.focus();
  }
  readonly destination = input.required<string>();
  readonly name = input.required<string>();
  readonly portfolioUrl = input.required<string>();
  readonly summary = input<string | null>(null);
  readonly tags = input<string[]>([]);
  readonly referenceCount = input(0);
  readonly references = input<{ id: string; title: string; previewUrl: string | null }[]>([]);
  readonly failedImages = signal<string[]>([]);
  readonly portfolio = computed(() => {
    try {
      const url = new URL(this.portfolioUrl());
      return ['http:', 'https:'].includes(url.protocol)
        ? { url: this.portfolioUrl(), host: url.hostname }
        : null;
    } catch {
      return null;
    }
  });
  imageFailed(id: string): void {
    this.failedImages.update((ids) => [...ids, id]);
  }
}
