import {
  afterNextRender,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { COMPARISON_SERVICE, ComparisonResult, ServiceError } from 'api';
import { CritiqueContent } from 'components';
import { presentCritique } from '../photograph-critique/present-critique';

@Component({
  selector: 'lp-attempt-comparison',
  imports: [DatePipe, CritiqueContent],
  templateUrl: './attempt-comparison.html',
  styleUrl: './attempt-comparison.css',
})
export class AttemptComparison {
  readonly firstId = input.required<string>();
  readonly secondId = input.required<string>();
  private readonly service = inject(COMPARISON_SERVICE);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly attempt = signal(0);
  readonly result = signal<ComparisonResult | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly sides = computed(() => {
    const result = this.result();
    return result
      ? [
          {
            label: 'First attempt',
            ...result.first,
            content: presentCritique(result.first.critique),
          },
          {
            label: 'Second attempt',
            ...result.second,
            content: presentCritique(result.second.critique),
          },
        ]
      : [];
  });
  readonly briefFields = [
    { key: 'intent', label: 'Intent' },
    { key: 'genre', label: 'Genre' },
    { key: 'experience', label: 'Experience' },
    { key: 'requestedFeedback', label: 'Requested feedback' },
  ] as const;

  constructor() {
    let previousFirstId: string | null = null;
    let previousSecondId: string | null = null;
    let previousAttempt = 0;
    effect((onCleanup) => {
      const firstId = this.firstId(),
        secondId = this.secondId(),
        attempt = this.attempt();
      const focusAfterRetry =
        attempt !== previousAttempt && firstId === previousFirstId && secondId === previousSecondId;
      previousFirstId = firstId;
      previousSecondId = secondId;
      previousAttempt = attempt;
      let active = true;
      onCleanup(() => {
        active = false;
      });
      this.loading.set(true);
      this.error.set(null);
      this.result.set(null);
      void this.service
        .load(firstId, secondId)
        .then((result) => {
          if (active) this.result.set(result);
        })
        .catch((error) => {
          if (active) this.error.set(error instanceof ServiceError ? error.code : 'request_failed');
        })
        .finally(() => {
          if (!active) return;
          this.loading.set(false);
          if (focusAfterRetry)
            afterNextRender(
              () => {
                if (active) this.heading()?.nativeElement.focus();
              },
              { injector: this.injector },
            );
        });
    });
  }
  retry(): void {
    this.attempt.update((value) => value + 1);
  }
}
