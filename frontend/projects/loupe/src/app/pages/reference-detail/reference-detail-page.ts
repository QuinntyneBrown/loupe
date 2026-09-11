import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';
import { Component, inject, input, signal, viewChild } from '@angular/core';
import { ReferenceResult } from 'api';
import { ReferenceImageDialog } from '../../dialogs/reference-image/reference-image-dialog';
import { Router, RouterLink } from '@angular/router';
import { DeleteReference } from '../../dialogs/delete-reference/delete-reference';
import { BoardPicker } from '../../dialogs/board-picker/board-picker';
import { ReferenceDetailPanel } from 'domain';
@Component({
  selector: 'lp-reference-detail-page',
  imports: [
    RouterLink,
    ReferenceDetailPanel,
    UnsavedChanges,
    ReferenceImageDialog,
    DeleteReference,
    BoardPicker,
  ],
  templateUrl: './reference-detail-page.html',
  styleUrl: './reference-detail-page.css',
})
export class ReferenceDetailPage {
  readonly pickingBoards = signal<ReferenceResult | null>(null);
  closeBoards(saved?: ReferenceResult): void {
    if (saved) this.detail()?.reference.set(saved);
    this.pickingBoards.set(null);
    this.detail()?.focusBoards();
  }
  readonly deletingReference = signal<ReferenceResult | null>(null);
  readonly deletionDialog = viewChild(DeleteReference);
  private readonly router = inject(Router);
  private deleted = false;
  closeDeletion(): void {
    this.deletingReference.set(null);
    this.detail()?.focusActions();
  }
  async deletionComplete(): Promise<void> {
    this.deleted = true;
    this.deletingReference.set(null);
    await this.router.navigate(['/inspiration'], { state: { referenceDeleted: true } });
  }
  readonly replacingImage = signal<ReferenceResult | null>(null);
  readonly imageDialog = viewChild(ReferenceImageDialog);
  closeImage(saved?: ReferenceResult): void {
    if (saved) this.detail()?.reference.set(saved);
    this.replacingImage.set(null);
    this.detail()?.focusActions();
  }
  readonly detail = viewChild(ReferenceDetailPanel);
  private readonly dialog = viewChild.required(UnsavedChanges);
  readonly dirty = () =>
    !this.deleted &&
    !!(this.detail()?.dirty() || this.imageDialog()?.dirty() || this.deletionDialog()?.busy());
  canLeave(): boolean | Promise<boolean> {
    return this.dialog().canLeave(this.dirty());
  }
  async requestDiscard(discard: () => void): Promise<void> {
    if (await this.dialog().confirmDiscard()) discard();
  }
  readonly id = input.required<string>();
}
