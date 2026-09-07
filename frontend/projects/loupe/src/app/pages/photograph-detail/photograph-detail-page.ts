import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PhotographDetail } from 'domain';

@Component({
  selector: 'lp-photograph-detail-page',
  imports: [RouterLink, PhotographDetail],
  templateUrl: './photograph-detail-page.html',
  styleUrl: './photograph-detail-page.css',
})
export class PhotographDetailPage {
  readonly id = input.required<string>();
}
