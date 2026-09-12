import { Component, computed, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'lp-reference-card',
  imports: [RouterLink],
  templateUrl: './reference-card.html',
  styleUrl: './reference-card.css',
})
export class ReferenceCard {
  readonly boardActions = input(true);
  readonly inBoard = input(false);
  readonly busy = input(false);
  readonly boardsRequested = output<void>();
  readonly removeRequested = output<void>();
  readonly destination = input.required<string>();
  readonly title = input.required<string>();
  readonly attribution = input<string | null>(null);
  readonly sourceUrl = input<string | null>(null);
  readonly previewUrl = input<string | null>(null);
  readonly width = input<number | null>(null);
  readonly height = input<number | null>(null);
  readonly failedImage = signal<string | null>(null);
  readonly source = computed(() => {
    try {
      const url = new URL(this.sourceUrl() ?? '');
      return ['https:', 'http:'].includes(url.protocol) ? this.sourceUrl() : null;
    } catch {
      return null;
    }
  });
  private readonly link = viewChild<ElementRef<HTMLAnchorElement>>('link');
  focus(): void {
    this.link()?.nativeElement.focus();
  }
}
