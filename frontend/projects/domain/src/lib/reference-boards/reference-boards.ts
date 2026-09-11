import {
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { BOARD_SERVICE, BoardResult, ReferenceResult } from 'api';

@Component({
  selector: 'lp-reference-boards',
  imports: [RouterLink],
  templateUrl: './reference-boards.html',
  styleUrl: './reference-boards.css',
})
export class ReferenceBoards {
  readonly reference = input.required<ReferenceResult>();
  readonly disabled = input(false);
  readonly pickerRequested = output<ReferenceResult>();
  readonly boards = signal<BoardResult[]>([]);
  readonly memberships = computed(() =>
    this.boards().filter((board) => this.reference().boardIds.includes(board.id)),
  );
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly attempt = signal(0);
  retry(): void {
    this.attempt.update((value) => value + 1);
  }
  private readonly service = inject(BOARD_SERVICE);
  private readonly button = viewChild.required<ElementRef<HTMLButtonElement>>('button');
  constructor() {
    effect((onCleanup) => {
      this.reference();
      this.attempt();
      let active = true;
      onCleanup(() => {
        active = false;
      });
      this.loading.set(true);
      this.failed.set(false);
      void this.service
        .list()
        .then((boards) => {
          if (active) this.boards.set(boards);
        })
        .catch(() => {
          if (active) this.failed.set(true);
        })
        .finally(() => {
          if (active) this.loading.set(false);
        });
    });
  }
  focus(): void {
    this.button().nativeElement.focus();
  }
}
