import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DeletionStatus } from 'domain';

@Component({
  selector: 'lp-deletion-page',
  imports: [RouterLink, DeletionStatus],
  templateUrl: './deletion-page.html',
  styleUrl: './deletion-page.css',
})
export class DeletionPage {
  readonly id = input.required<string>();
}
