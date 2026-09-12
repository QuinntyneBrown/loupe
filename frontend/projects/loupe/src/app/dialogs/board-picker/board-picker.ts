import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  BOARD_SERVICE,
  BoardResult,
  REFERENCE_SERVICE,
  ReferenceResult,
  ReferenceSummary,
  ServiceError,
} from 'api';

@Component({
  selector: 'lp-board-picker',
  imports: [FormsModule],
  templateUrl: './board-picker.html',
  styleUrl: './board-picker.css',
})
export class BoardPicker {
  readonly reference = input.required<ReferenceSummary>();
  readonly closed = output<void>();
  readonly saved = output<ReferenceResult>();
  readonly boards = signal<BoardResult[]>([]);
  readonly selected = signal<string[]>([]);
  readonly current = signal<ReferenceResult | null>(null);
  readonly newName = signal('');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly conflicted = signal(false);
  private readonly service = inject(BOARD_SERVICE);
  private readonly references = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  constructor() {
    afterNextRender(() => {
      this.modal().nativeElement.showModal();
      void this.load();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.saving()) {
      this.modal().nativeElement.close();
      this.closed.emit();
    }
  }
  toggle(id: string): void {
    this.selected.update((ids) =>
      ids.includes(id) ? ids.filter((value) => value !== id) : [...ids, id],
    );
  }
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [boards, reference] = await Promise.all([
        this.service.list(),
        this.references.get(this.reference().id),
      ]);
      if (this.destroy.destroyed) return;
      const pending = this.current() !== null;
      // Keep the user's selection across a revision conflict; only refresh its base.
      if (!pending) this.selected.set(reference.boardIds);
      this.boards.set(boards);
      this.current.set(reference);
      this.conflicted.set(false);
    } catch {
      if (!this.destroy.destroyed) this.error.set('Boards could not be loaded. Try again.');
    } finally {
      if (!this.destroy.destroyed) this.loading.set(false);
    }
  }
  async save(): Promise<void> {
    const reference = this.current();
    if (!reference || this.saving() || this.loading() || this.conflicted()) return;
    const name = this.newName().trim().normalize('NFC');
    if ([...name].length > 80) {
      this.error.set('Use 80 characters or fewer for the new board.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    try {
      if (name) {
        const board = await this.service.create(name);
        if (this.destroy.destroyed) return;
        this.boards.update((boards) =>
          [...boards, board].sort((a, b) => a.name.localeCompare(b.name)),
        );
        this.selected.update((ids) => [...ids, board.id]);
        this.newName.set('');
      }
      const saved = await this.service.setMemberships(
        reference.id,
        reference.revision,
        this.selected(),
      );
      if (!this.destroy.destroyed) {
        this.modal().nativeElement.close();
        this.saved.emit(saved);
      }
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.conflicted.set(code === 'revision_conflict');
      this.error.set(
        code === 'board_name_conflict'
          ? 'A board with this name already exists. Select it above or choose another name.'
          : code === 'revision_conflict'
            ? 'This reference changed. Load the latest version before saving.'
            : code === 'item_unavailable'
              ? 'A selected board or reference is no longer available. Close and reopen the picker.'
              : 'Board changes could not be saved. Your selection is still here. Try again.',
      );
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
