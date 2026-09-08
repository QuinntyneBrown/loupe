import { Component, ElementRef, input, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'lp-photograph-card',
  imports: [DatePipe, RouterLink],
  templateUrl: './photograph-card.html',
  styleUrl: './photograph-card.css',
})
export class PhotographCard {
  readonly destination = input.required<string>();
  private readonly link = viewChild<ElementRef<HTMLAnchorElement>>('link');
  focus(): void {
    this.link()?.nativeElement.focus();
  }
  readonly title = input.required<string>();
  readonly status = input.required<string>();
  readonly previewUrl = input.required<string | null>();
  readonly createdAt = input.required<string>();
  readonly width = input.required<number | null>();
  readonly height = input.required<number | null>();
}
