import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReferenceDetailPanel } from 'domain';
@Component({
  selector: 'lp-reference-detail-page',
  imports: [RouterLink, ReferenceDetailPanel],
  templateUrl: './reference-detail-page.html',
  styleUrl: './reference-detail-page.css',
})
export class ReferenceDetailPage {
  readonly id = input.required<string>();
}
