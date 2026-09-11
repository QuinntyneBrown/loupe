import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PhotographerDetail } from 'domain';
@Component({
  selector: 'lp-photographer-detail-page',
  imports: [RouterLink, PhotographerDetail],
  templateUrl: './photographer-detail-page.html',
  styleUrl: './photographer-detail-page.css',
})
export class PhotographerDetailPage {
  readonly id = input.required<string>();
}
