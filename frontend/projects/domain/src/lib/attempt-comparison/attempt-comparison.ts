import {
  afterNextRender,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  untracked,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { COMPARISON_SERVICE, ComparisonAttempt, ServiceError } from 'api';
import { CritiqueContent } from 'components';
import { presentCritique } from '../photograph-critique/present-critique';
import { ComparisonView } from './comparison-view';

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
  readonly replacementRequested = output<'first' | 'second'>();
  readonly result = signal<ComparisonView | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly sides = computed(() => {
    const result = this.result();
    return result
      ? (['first', 'second'] as const).map((key) => ({
          key,
          label: key === 'first' ? 'First attempt' : 'Second attempt',
          item: result[key],
          content: result[key] ? presentCritique(result[key].critique) : null,
        }))
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
      const samePair = firstId === previousFirstId && secondId === previousSecondId;
      const previous = untracked(this.result);
      previousFirstId = firstId;
      previousSecondId = secondId;
      previousAttempt = attempt;
      let active = true;
      onCleanup(() => {
        active = false;
      });
      this.loading.set(true);
      this.error.set(null);
      if (!samePair) this.result.set(null);
      void this.service
        .load(firstId, secondId)
        .then((result) => {
          if (active) this.result.set(result);
        })
        .catch(async (error) => {
          if (!active) return;
          const code = error instanceof ServiceError ? error.code : 'request_failed';
          if (code === 'item_unavailable' && previous) {
            await this.recover(firstId, secondId, previous, () => active);
          } else this.error.set(code);
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
  private async recover(
    firstId: string,
    secondId: string,
    previous: ComparisonView,
    active: () => boolean,
  ): Promise<void> {
    const attempts = await Promise.allSettled([
      this.service.readAttempt(firstId),
      this.service.readAttempt(secondId),
    ]);
    if (!active()) return;
    let failed = false;
    const side = (
      attempt: PromiseSettledResult<ComparisonAttempt>,
      old: ComparisonAttempt | null,
      id: string,
    ): ComparisonAttempt | null => {
      if (attempt.status === 'fulfilled') return attempt.value;
      if (
        attempt.reason instanceof ServiceError &&
        ['item_unavailable', 'invalid_request'].includes(attempt.reason.code)
      )
        return null;
      failed = true;
      return old?.photograph.id === id ? old : null;
    };
    this.result.set({
      first: side(attempts[0], previous.first, firstId),
      second: side(attempts[1], previous.second, secondId),
    });
    this.error.set(failed ? 'recovery_failed' : null);
  }
  retry(): void {
    this.attempt.update((value) => value + 1);
  }
}
