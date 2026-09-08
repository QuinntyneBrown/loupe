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
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { CRITIQUE_SERVICE, SavedCritique, ServiceError } from 'api';

@Component({
  selector: 'lp-photograph-critique',
  imports: [DatePipe, NgTemplateOutlet],
  templateUrl: './photograph-critique.html',
  styleUrl: './photograph-critique.css',
})
export class PhotographCritique {
  readonly id = input.required<string>();
  readonly operationId = input<string | null>(null);
  readonly regenerationBlocked = input(false);
  readonly regenerationRequested = output<void>();
  private readonly service = inject(CRITIQUE_SERVICE);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly attempt = signal(0);
  readonly critique = signal<SavedCritique | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly hasBrief = computed(() => Object.values(this.critique()?.brief ?? {}).some(Boolean));
  readonly briefFields = [
    { key: 'intent', label: 'Intent' },
    { key: 'genre', label: 'Genre' },
    { key: 'experience', label: 'Experience' },
    { key: 'requestedFeedback', label: 'Requested feedback' },
  ] as const;
  readonly evidenceLabels: Record<string, string> = {
    VisibleObservation: 'Visible observation',
    ExifFact: 'EXIF fact',
    Hypothesis: 'Hypothesis',
    StylisticPreference: 'Stylistic preference',
  };
  readonly exifLabels: Record<string, string> = {
    Camera: 'Camera',
    Lens: 'Lens',
    Aperture: 'Aperture',
    ShutterSpeed: 'Shutter speed',
    Iso: 'ISO',
    FocalLength: 'Focal length',
    CapturedAt: 'Capture time',
  };
  readonly aspectGroups = [
    {
      label: 'Technical observations',
      fields: [
        { key: 'exposure', label: 'Exposure' },
        { key: 'focus', label: 'Focus' },
        { key: 'depthOfField', label: 'Depth of field' },
        { key: 'motion', label: 'Motion' },
        { key: 'lighting', label: 'Lighting' },
        { key: 'color', label: 'Color' },
        { key: 'processing', label: 'Processing' },
      ],
    },
    {
      label: 'Composition and expression',
      fields: [
        { key: 'framing', label: 'Framing' },
        { key: 'subjectSeparation', label: 'Subject separation' },
        { key: 'balance', label: 'Balance' },
        { key: 'visualHierarchy', label: 'Visual hierarchy' },
        { key: 'mood', label: 'Mood' },
      ],
    },
  ] as const;

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
