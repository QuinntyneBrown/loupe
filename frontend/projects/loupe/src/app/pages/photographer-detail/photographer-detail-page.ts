import { Component, inject, input, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PhotographerDetail } from 'domain';
import { PhotographerResult } from 'api';
import { EditPhotographer } from '../../dialogs/edit-photographer/edit-photographer';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';
import { DeletePhotographer } from '../../dialogs/delete-photographer/delete-photographer';
@Component({
  selector: 'lp-photographer-detail-page',
  imports: [RouterLink, PhotographerDetail, EditPhotographer, UnsavedChanges, DeletePhotographer],
  templateUrl: './photographer-detail-page.html',
  styleUrl: './photographer-detail-page.css',
})
export class PhotographerDetailPage {
  readonly id = input.required<string>();
  readonly editing = signal<PhotographerResult | null>(null);
  readonly deleting = signal<{ item: PhotographerResult; count: number } | null>(null);
  private readonly router = inject(Router);
  private readonly deleteDialog = viewChild(DeletePhotographer);
  private deleted = false;
  reviewed(item: PhotographerResult): void {
    this.detail().item.set(item);
  }
  async deletionComplete(): Promise<void> {
    this.deleted = true;
    this.deleting.set(null);
    await this.router.navigate(['/photographers'], { state: { photographerDeleted: true } });
  }
  private readonly detail = viewChild.required(PhotographerDetail);
  private readonly editDialog = viewChild(EditPhotographer);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  readonly dirty = () =>
    !this.deleted &&
    !!(this.editDialog()?.dirty() || this.deleteDialog()?.busy() || this.detail()?.dirty());
  canLeave(): boolean | Promise<boolean> {
    return this.unsaved().canLeave(this.dirty());
  }
  saved(item: PhotographerResult): void {
    this.detail().item.set(item);
    this.editing.set(null);
  }
}
