import { Component, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PhotographerDetail } from 'domain';
import { PhotographerResult } from 'api';
import { EditPhotographer } from '../../dialogs/edit-photographer/edit-photographer';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';
@Component({
  selector: 'lp-photographer-detail-page',
  imports: [RouterLink, PhotographerDetail, EditPhotographer, UnsavedChanges],
  templateUrl: './photographer-detail-page.html',
  styleUrl: './photographer-detail-page.css',
})
export class PhotographerDetailPage {
  readonly id = input.required<string>();
  readonly editing = signal<PhotographerResult | null>(null);
  private readonly detail = viewChild.required(PhotographerDetail);
  private readonly editDialog = viewChild(EditPhotographer);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  readonly dirty = () => !!this.editDialog()?.dirty();
  canLeave(): boolean | Promise<boolean> {
    return this.unsaved().canLeave(this.dirty());
  }
  saved(item: PhotographerResult): void {
    this.detail().item.set(item);
    this.editing.set(null);
  }
}
