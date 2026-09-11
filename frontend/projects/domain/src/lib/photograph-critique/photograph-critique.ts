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
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { CritiqueContent, EvidenceRegion } from 'components';
import { presentCritique } from './present-critique';
import { CRITIQUE_SERVICE, SavedCritique, ServiceError } from 'api';

@Component({
  selector: 'lp-photograph-critique',
  imports: [CritiqueContent],
  templateUrl: './photograph-critique.html',
  styleUrl: './photograph-critique.css',
})
export class PhotographCritique {
  readonly id = input.required<string>();
  readonly imageUrl = input<string | null>(null);
  readonly imageWidth = input(1);
  readonly imageHeight = input(1);
  readonly regionChanged = output<EvidenceRegion | null>();
  readonly operationId = input<string | null>(null);
  private readonly service = inject(CRITIQUE_SERVICE);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly attempt = signal(0);
  readonly critique = signal<SavedCritique | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly presentation = computed(() => {
    const item = this.critique();
    return item ? presentCritique(item) : null;
  });

  constructor() {
    let previousId: string | null = null;
    let previousAttempt = 0;
    effect((onCleanup) => {
      const id = this.id();
      this.operationId();
      const attempt = this.attempt();
      const focusAfterRetry = attempt !== previousAttempt && id === previousId;
      previousAttempt = attempt;
      let active = true;
      onCleanup(() => {
        active = false;
      });
      if (id !== previousId) this.critique.set(null);
      previousId = id;
      this.loading.set(!untracked(this.critique));
      this.error.set(null);
      void this.service
        .get(id)
        .then((result) => {
          if (active) this.critique.set(result);
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
              {
                injector: this.injector,
              },
            );
        });
    });
  }
  retry(): void {
    this.attempt.update((value) => value + 1);
  }
}
