import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BOARD_SERVICE, BoardResult, ServiceError } from 'api';

@Component({
  selector: 'lp-board-dialog',
  imports: [FormsModule],
  templateUrl: './board-dialog.html',
  styleUrl: './board-dialog.css',
})
export class BoardDialog {
  readonly board = input<BoardResult | null>(null);
  readonly deleting = input(false);
  readonly closed = output<void>();
  readonly saved = output<BoardResult | null>();
  readonly name = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly conflicted = signal(false);
  readonly current = signal<BoardResult | null>(null);
  readonly title = computed(() =>
    this.deleting()
      ? `Delete “${this.current()?.name ?? this.board()?.name}”?`
      : this.board()
        ? 'Rename board'
        : 'New board',
  );
  private readonly service = inject(BOARD_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  constructor() {
    afterNextRender(() => {
      this.current.set(this.board());
      this.name.set(this.board()?.name ?? '');
      this.modal().nativeElement.showModal();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy()) {
      this.modal().nativeElement.close();
      this.closed.emit();
    }
  }
  async reload(): Promise<void> {
    this.busy.set(true);
    try {
      const latest = (await this.service.list()).find((board) => board.id === this.board()?.id);
      if (this.destroy.destroyed) return;
      if (!latest) {
        this.error.set('This board is no longer available.');
        return;
      }
      this.current.set(latest);
      this.conflicted.set(false);
      this.error.set('Latest board loaded. Review and save again.');
    } catch {
      if (!this.destroy.destroyed)
        this.error.set('The latest board could not be loaded. Try again.');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async save(): Promise<void> {
    if (this.busy() || this.conflicted()) return;
    const name = this.name().trim().normalize('NFC');
    if (!this.deleting() && (!name || [...name].length > 80)) {
      this.error.set('Enter a board name of 1–80 characters.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    try {
      const board = this.current();
      let result: BoardResult | null;
      if (this.deleting() && board) {
        await this.service.delete(board.id, board.revision);
        result = null;
      } else
        result = board
          ? await this.service.rename(board.id, board.revision, name)
          : await this.service.create(name);
      if (!this.destroy.destroyed) {
        this.modal().nativeElement.close();
        this.saved.emit(result);
      }
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.conflicted.set(code === 'revision_conflict');
      this.error.set(
        code === 'board_name_conflict'
          ? 'A board with this name already exists. Choose another name.'
          : code === 'revision_conflict'
            ? 'This board changed. Load the latest board before saving.'
            : code === 'item_unavailable'
              ? 'This board is no longer available.'
              : 'The change could not be saved. Your draft is still here. Try again.',
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
