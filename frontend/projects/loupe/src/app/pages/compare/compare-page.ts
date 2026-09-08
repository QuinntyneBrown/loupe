import { Component, effect, inject, input, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AttemptComparison, AttemptPicker } from 'domain';
import { ReplaceAttempt } from '../../dialogs/replace-attempt/replace-attempt';

@Component({
  selector: 'lp-compare-page',
  imports: [RouterLink, AttemptComparison, AttemptPicker, ReplaceAttempt],
  templateUrl: './compare-page.html',
  styleUrl: './compare-page.css',
})
export class ComparePage {
  private readonly router = inject(Router);
  private readonly replacement = viewChild(ReplaceAttempt);
  readonly firstId = input('');
  readonly secondId = input('');
  constructor() {
    effect(() => {
      this.firstId();
      this.secondId();
      this.replacement()?.cancel();
    });
  }
  replace(selection: { side: 'first' | 'second'; id: string }): void {
    void this.router.navigate(['/compare'], {
      queryParams: {
        firstId: selection.side === 'first' ? selection.id : this.firstId(),
        secondId: selection.side === 'second' ? selection.id : this.secondId(),
      },
    });
  }
  choose(id: string): void {
    void this.router.navigate(['/compare'], {
      queryParams: this.firstId() ? { firstId: this.firstId(), secondId: id } : { firstId: id },
    });
  }
}
