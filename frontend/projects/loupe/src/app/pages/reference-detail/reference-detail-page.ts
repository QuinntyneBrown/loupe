import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';
import { Component, input, signal, viewChild } from '@angular/core';
import { ReferenceResult } from 'api';
import { ReferenceImageDialog } from '../../dialogs/reference-image/reference-image-dialog';
import { RouterLink } from '@angular/router';
import { ReferenceDetailPanel } from 'domain';
@Component({
  selector: 'lp-reference-detail-page',
  imports: [RouterLink, ReferenceDetailPanel, UnsavedChanges, ReferenceImageDialog],
  templateUrl: './reference-detail-page.html',
  styleUrl: './reference-detail-page.css',
})
export class ReferenceDetailPage {
  readonly replacingImage = signal<ReferenceResult | null>(null);
  readonly imageDialog = viewChild(ReferenceImageDialog);
  closeImage(saved?: ReferenceResult): void {
    if (saved) this.detail()?.reference.set(saved);
    this.replacingImage.set(null);
    this.detail()?.focusActions();
  }
  readonly detail = viewChild(ReferenceDetailPanel);
  private readonly dialog = viewChild.required(UnsavedChanges);
  readonly dirty = () => !!(this.detail()?.dirty() || this.imageDialog()?.dirty());
  canLeave(): boolean | Promise<boolean> {
    return this.dialog().canLeave(this.dirty());
  }
  async requestDiscard(discard: () => void): Promise<void> {
    if (await this.dialog().confirmDiscard()) discard();
  }
  readonly id = input.required<string>();
}
