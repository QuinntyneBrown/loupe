import { Component, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AttemptComparison, AttemptPicker } from 'domain';

@Component({
  selector: 'lp-compare-page',
  imports: [RouterLink, AttemptComparison, AttemptPicker],
  templateUrl: './compare-page.html',
  styleUrl: './compare-page.css',
})
export class ComparePage {
  private readonly router = inject(Router);
  readonly firstId = input('');
  readonly secondId = input('');
  choose(id: string): void {
    void this.router.navigate(['/compare'], {
      queryParams: this.firstId() ? { firstId: this.firstId(), secondId: id } : { firstId: id },
    });
  }
}
