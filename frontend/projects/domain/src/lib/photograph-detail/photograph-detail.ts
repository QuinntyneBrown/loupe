import {
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { PHOTOGRAPH_SERVICE, PhotographResult, OperationResult, ServiceError } from 'api';
import { PhotographNotes } from '../photograph-notes/photograph-notes';
import { PhotographBrief } from '../photograph-brief/photograph-brief';
import { PhotographCritique } from '../photograph-critique/photograph-critique';
import { CritiqueStatus } from '../critique-status/critique-status';

@Component({
  selector: 'lp-photograph-detail',
  imports: [
    RouterLink,
    DatePipe,
    PhotographNotes,
    PhotographBrief,
    PhotographCritique,
    CritiqueStatus,
  ],
  templateUrl: './photograph-detail.html',
  styleUrl: './photograph-detail.css',
})
export class PhotographDetail {
  readonly id = input.required<string>();
  readonly discardRequested = output<() => void>();
  readonly deleteRequested = output<PhotographResult>();
  readonly regenerateRequested = output<PhotographResult>();
  private readonly critiquePanel = viewChild(PhotographCritique);
  readonly hasCritique = computed(() => !!this.critiquePanel()?.critique());
  private readonly critiqueStatus = viewChild(CritiqueStatus);
  readonly regenerationBlocked = computed(() => {
    const status = this.critiqueStatus();
    return (
      !status ||
      status.loading() ||
      !!status.error() ||
      this.briefDirty() ||
      ['Queued', 'Running'].includes(status.operation()?.status ?? '')
    );
  });
  private readonly notesEditor = viewChild(PhotographNotes);
  private readonly briefEditor = viewChild(PhotographBrief);
  readonly dirty = computed(() => !!(this.notesEditor()?.dirty() || this.briefEditor()?.dirty()));
  readonly briefDirty = computed(
    () => !!(this.briefEditor()?.dirty() || this.briefEditor()?.busy()),
  );
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  readonly photo = signal<PhotographResult | null>(null);
  readonly completedCritiqueId = signal<string | null>(null);
  readonly hasCaptureSettings = computed(() =>
    Object.values(this.photo()?.exif ?? {}).some(Boolean),
  );
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  private readonly attempt = signal(0);
  readonly captureFields = [
    { key: 'camera', label: 'Camera' },
    { key: 'lens', label: 'Lens' },
    { key: 'aperture', label: 'Aperture' },
    { key: 'shutterSpeed', label: 'Shutter speed' },
    { key: 'iso', label: 'ISO' },
    { key: 'focalLength', label: 'Focal length' },
    { key: 'capturedAt', label: 'Capture time (camera)' },
  ] as const;

  constructor() {
    effect((onCleanup) => {
      const id = this.id();
      this.attempt();
      let active = true;
      onCleanup(() => {
        active = false;
      });
      this.loading.set(true);
      this.error.set(null);
      this.photo.set(null);
      void this.service
        .get(id)
        .then((photo) => {
          if (active) this.photo.set(photo);
        })
        .catch((error) => {
          if (active) this.error.set(error instanceof ServiceError ? error.code : 'request_failed');
        })
        .finally(() => {
          if (active) this.loading.set(false);
        });
    });
  }
  retry(): void {
    this.attempt.update((value) => value + 1);
  }
  followCritique(operation: OperationResult): void {
    if (operation.resourceId !== this.id()) return;
    this.critiqueStatus()?.admitted.set(operation);
    this.critiqueStatus()?.focus();
  }
}
