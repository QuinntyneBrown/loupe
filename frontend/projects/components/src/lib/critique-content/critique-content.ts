import { Component, input } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { CritiquePresentation } from './critique-presentation';

@Component({
  selector: 'lp-critique-content',
  imports: [DatePipe, NgTemplateOutlet],
  templateUrl: './critique-content.html',
  styleUrl: './critique-content.css',
})
export class CritiqueContent {
  readonly content = input.required<CritiquePresentation>();
}
