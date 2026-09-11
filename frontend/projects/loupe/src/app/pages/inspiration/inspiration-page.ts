import { Component, computed, DestroyRef, inject, input, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { BoardNavigation, ReferenceCollection, ReferenceFilters } from 'domain';
import {
  BOARD_SERVICE,
  BoardResult,
  REFERENCE_SERVICE,
  ReferenceResult,
  ReferenceSummary,
  ReferenceTagFacet,
} from 'api';
import { BoardDialog } from '../../dialogs/board/board-dialog';
import { BoardPicker } from '../../dialogs/board-picker/board-picker';
import { TagFiltersDialog } from '../../dialogs/tag-filters/tag-filters-dialog';
import { SaveReference } from '../../dialogs/save-reference/save-reference';
@Component({
  selector: 'lp-inspiration-page',
  imports: [
    ReferenceCollection,
    BoardNavigation,
    BoardDialog,
    BoardPicker,
    ReferenceFilters,
    TagFiltersDialog,
    SaveReference,
  ],
  templateUrl: './inspiration-page.html',
  styleUrl: './inspiration-page.css',
})
export class InspirationPage {
  readonly savingReference = signal(false);
  referenceSaved(reference: ReferenceResult): void {
    this.savingReference.set(false);
    this.notice.set(`“${reference.title}” saved.`);
    this.refresh();
  }
  readonly tags = input<string[], string | string[] | undefined>([], {
    transform: (value) => (value === undefined ? [] : Array.isArray(value) ? value : [value]),
  });
  readonly tagDialog = signal<ReferenceTagFacet[] | null>(null);
  async filterTags(tags: string[]): Promise<void> {
    this.tagDialog.set(null);
    await this.router.navigate([], {
      queryParams: { tags: tags.length ? tags : null },
      queryParamsHandling: 'merge',
    });
  }
  readonly boardId = input<string | null, string | undefined>(null, {
    transform: (value) => value ?? null,
  });
  readonly boards = signal<BoardResult[]>([]);
  readonly libraryCount = signal<number | null>(null);
  readonly selectedBoard = computed(
    () => this.boards().find((board) => board.id === this.boardId()) ?? null,
  );
  readonly editor = signal<{ board: BoardResult | null; deleting: boolean } | null>(null);
  readonly notice = signal('');
  readonly picker = signal<ReferenceSummary | null>(null);
  readonly busy = signal(false);
  readonly failure = signal('');
  readonly undo = signal<{ referenceId: string; boardId: string; boardName: string } | null>(null);
  readonly navigation = viewChild.required(BoardNavigation);
  readonly collection = viewChild.required(ReferenceCollection);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly references = inject(REFERENCE_SERVICE);
  private readonly boardService = inject(BOARD_SERVICE);
  editBoard(board: BoardResult | null = null, deleting = false): void {
    this.editor.set({ board, deleting });
  }
  async boardSaved(board: BoardResult | null): Promise<void> {
    const wasExisting = !!this.editor()?.board;
    this.editor.set(null);
    this.notice.set(
      board
        ? wasExisting
          ? 'Board renamed.'
          : `Board “${board.name}” created.`
        : 'Board deleted. Its references are still in your library.',
    );
    if (!board) await this.router.navigate(['/inspiration']);
    if (!this.destroy.destroyed) await this.navigation().refresh();
  }
  async membershipSaved(reference: ReferenceResult): Promise<void> {
    this.picker.set(null);
    if (this.boardId() && !reference.boardIds.includes(this.boardId()!))
      this.collection().refresh();
    await this.navigation().refresh();
    if (this.destroy.destroyed) return;
    const names = this.boards()
      .filter((board) => reference.boardIds.includes(board.id))
      .map((board) => board.name);
    this.notice.set(
      names.length ? `Added to ${names.join(' and ')}.` : 'Board memberships updated.',
    );
  }
  private refresh(): void {
    this.collection().refresh();
    void this.navigation().refresh();
  }
  async remove(reference: ReferenceSummary): Promise<void> {
    const board = this.selectedBoard();
    if (!board || this.busy()) return;
    this.busy.set(true);
    this.failure.set('');
    try {
      const latest = await this.references.get(reference.id);
      if (this.destroy.destroyed) return;
      await this.boardService.setMemberships(
        latest.id,
        latest.revision,
        latest.boardIds.filter((id) => id !== board.id),
      );
      if (this.destroy.destroyed) return;
      this.undo.set({ referenceId: reference.id, boardId: board.id, boardName: board.name });
      this.notice.set(`Removed from ${board.name}.`);
      this.refresh();
    } catch {
      if (!this.destroy.destroyed)
        this.failure.set('The reference could not be removed from the board. Try again.');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async undoRemoval(): Promise<void> {
    const undo = this.undo();
    if (!undo || this.busy()) return;
    this.busy.set(true);
    this.failure.set('');
    try {
      const latest = await this.references.get(undo.referenceId);
      if (this.destroy.destroyed) return;
      await this.boardService.setMemberships(latest.id, latest.revision, [
        ...new Set([...latest.boardIds, undo.boardId]),
      ]);
      if (this.destroy.destroyed) return;
      this.undo.set(null);
      this.notice.set(`Restored to ${undo.boardName}.`);
      this.refresh();
    } catch {
      if (!this.destroy.destroyed)
        this.failure.set(
          'The reference could not be restored. The board or reference may have changed. Try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
