import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';
import { Component, input, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReferenceDetailPanel } from 'domain';
@Component({
  selector: 'lp-reference-detail-page',
  imports: [RouterLink, ReferenceDetailPanel, UnsavedChanges],
  templateUrl: './reference-detail-page.html',
  styleUrl: './reference-detail-page.css',
})
export class ReferenceDetailPage {
  readonly detail = viewChild(ReferenceDetailPanel);
  private readonly dialog = viewChild.required(UnsavedChanges);
  readonly dirty = () => this.detail()?.dirty() ?? false;
  canLeave(): boolean | Promise<boolean> {
    return this.dialog().canLeave(this.dirty());
  }
  async requestDiscard(discard: () => void): Promise<void> {
    if (await this.dialog().confirmDiscard()) discard();
  }
  readonly id = input.required<string>();
}
