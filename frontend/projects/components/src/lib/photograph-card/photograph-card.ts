import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'lp-photograph-card',
  imports: [DatePipe],
  templateUrl: './photograph-card.html',
  styleUrl: './photograph-card.css',
})
export class PhotographCard {
  readonly title = input.required<string>();
  readonly previewUrl = input.required<string>();
  readonly createdAt = input.required<string>();
  readonly width = input.required<number>();
  readonly height = input.required<number>();
}
