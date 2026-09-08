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
  viewChildren,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { COMPARISON_SERVICE, PhotographSummary } from 'api';

@Component({
  selector: 'lp-attempt-picker',
  imports: [DatePipe],
  templateUrl: './attempt-picker.html',
  styleUrl: './attempt-picker.css',
})
export class AttemptPicker {
  readonly excludeId = input<string | null>(null);
  readonly chosen = output<string>();
  private readonly service = inject(COMPARISON_SERVICE);
  private readonly injector = inject(Injector);
  private readonly choices = viewChildren<ElementRef<HTMLButtonElement>>('choice');
  private generation = 0;
  readonly items = signal<PhotographSummary[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly available = computed(() => this.items().filter((item) => item.id !== this.excludeId()));
  readonly tooFew = computed(() => !this.nextCursor() && this.items().length < 2);

  constructor() {
    effect((onCleanup) => {
      this.excludeId();
      this.generation++;
      onCleanup(() => {
        this.generation++;
      });
      this.items.set([]);
      this.nextCursor.set(null);
      this.loading.set(false);
      untracked(() => {
        void this.load(false);
      });
    });
  }
  async load(focus = true): Promise<void> {
    if (this.loading()) return;
    const generation = this.generation;
    const previous = new Set(this.items().map((item) => item.id));
    this.loading.set(true);
    this.error.set(false);
    let focusId: string | undefined;
    try {
      const page = await this.service.eligible(this.nextCursor() ?? undefined);
      if (generation !== this.generation) return;
      const added = page.items.filter((item) => !previous.has(item.id));
      this.items.update((items) => [...items, ...added]);
      this.nextCursor.set(page.nextCursor);
      focusId = added.find((item) => item.id !== this.excludeId())?.id;
    } catch {
      if (generation === this.generation) this.error.set(true);
    } finally {
      if (generation === this.generation) {
        this.loading.set(false);
        if (focus && focusId)
          afterNextRender(
            () => {
              if (generation === this.generation)
                this.choices()
                  .find((choice) => choice.nativeElement.dataset['id'] === focusId)
                  ?.nativeElement.focus();
            },
            { injector: this.injector },
          );
      }
    }
  }
}
