import { ReferenceMetadataEditor } from '../reference-metadata/reference-metadata-editor';
import { computed, output } from '@angular/core';
import {
  afterNextRender,
  Component,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { REFERENCE_SERVICE, ReferenceResult, ServiceError } from 'api';

@Component({
  selector: 'lp-reference-detail-panel',
  imports: [DatePipe, ReferenceMetadataEditor],
  templateUrl: './reference-detail-panel.html',
  styleUrl: './reference-detail-panel.css',
})
export class ReferenceDetailPanel {
  readonly discardRequested = output<() => void>();
  private readonly editor = viewChild(ReferenceMetadataEditor);
  readonly dirty = computed(() => !!(this.editor()?.dirty() || this.editor()?.busy()));
  readonly saving = computed(() => !!this.editor()?.saving());
  readonly id = input.required<string>();
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly injector = inject(Injector);
  readonly reference = signal<ReferenceResult | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly attempt = signal(0);
  constructor() {
    let previousId: string | undefined;
    effect((onCleanup) => {
      const id = this.id();
      const focusResult = this.attempt() > 0 && previousId === id;
      previousId = id;
      let active = true;
      onCleanup(() => (active = false));
      this.reference.set(null);
      this.loading.set(true);
      this.error.set('');
      void this.service
        .get(id)
        .then((item) => {
          if (active) this.reference.set(item);
        })
        .catch((error) => {
          if (active)
            this.error.set(
              error instanceof ServiceError && error.code === 'item_unavailable'
                ? 'Reference unavailable. It may have been removed.'
                : 'The reference could not be loaded. Try again.',
            );
        })
        .finally(() => {
          if (!active) return;
          this.loading.set(false);
          if (focusResult)
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
