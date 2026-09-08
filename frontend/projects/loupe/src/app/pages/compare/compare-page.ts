import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AttemptComparison } from 'domain';

@Component({
  selector: 'lp-compare-page',
  imports: [RouterLink, AttemptComparison],
  templateUrl: './compare-page.html',
  styleUrl: './compare-page.css',
})
export class ComparePage {
  readonly firstId = input('');
  readonly secondId = input('');
}
