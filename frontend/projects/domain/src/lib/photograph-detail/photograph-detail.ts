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
import { DatePipe } from '@angular/common';
import { PHOTOGRAPH_SERVICE, PhotographResult, ServiceError } from 'api';
import { PhotographNotes } from '../photograph-notes/photograph-notes';
import { PhotographBrief } from '../photograph-brief/photograph-brief';

@Component({
  selector: 'lp-photograph-detail',
  imports: [DatePipe, PhotographNotes, PhotographBrief],
  templateUrl: './photograph-detail.html',
  styleUrl: './photograph-detail.css',
})
export class PhotographDetail {
  readonly id = input.required<string>();
  readonly discardRequested = output<() => void>();
  private readonly notesEditor = viewChild(PhotographNotes);
  private readonly briefEditor = viewChild(PhotographBrief);
  readonly dirty = computed(() => !!(this.notesEditor()?.dirty() || this.briefEditor()?.dirty()));
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  readonly photo = signal<PhotographResult | null>(null);
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
}
