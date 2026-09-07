import { Component, computed, input, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PhotographDetail } from 'domain';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-photograph-detail-page',
  imports: [RouterLink, PhotographDetail, UnsavedChanges],
  templateUrl: './photograph-detail-page.html',
  styleUrl: './photograph-detail-page.css',
})
export class PhotographDetailPage {
  readonly id = input.required<string>();
  private readonly detail = viewChild(PhotographDetail);
  private readonly dialog = viewChild.required(UnsavedChanges);
  readonly dirty = computed(() => this.detail()?.dirty() ?? false);
  canLeave(): boolean | Promise<boolean> {
    return this.dialog().canLeave(this.dirty());
  }
  async requestDiscard(discard: () => void): Promise<void> {
    if (await this.dialog().confirmDiscard()) discard();
  }
}
