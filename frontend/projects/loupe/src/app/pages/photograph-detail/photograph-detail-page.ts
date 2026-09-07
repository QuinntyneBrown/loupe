import { Component, computed, inject, input, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DeletionResult, SESSION_SERVICE } from 'api';
import { PhotographDetail } from 'domain';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';
import { DeletePhotograph } from '../../dialogs/delete-photograph/delete-photograph';

@Component({
  selector: 'lp-photograph-detail-page',
  imports: [RouterLink, PhotographDetail, UnsavedChanges, DeletePhotograph],
  templateUrl: './photograph-detail-page.html',
  styleUrl: './photograph-detail-page.css',
})
export class PhotographDetailPage {
  readonly id = input.required<string>();
  private readonly detail = viewChild(PhotographDetail);
  private readonly dialog = viewChild.required(UnsavedChanges);
  private readonly router = inject(Router);
  private readonly session = inject(SESSION_SERVICE);
  private readonly deleteDialog = viewChild.required(DeletePhotograph);
  readonly deletion = signal<DeletionResult | null>(null);
  readonly dirty = computed(() => !this.deletion() && (this.detail()?.dirty() ?? false));
  readonly deleting = computed(() => !this.deletion() && this.deleteDialog().busy());
  readonly protectUnload = () => this.dirty() || this.deleting();
  onDeleted(result: DeletionResult): void {
    this.deletion.set(result);
    void this.router.navigate(['/deletions', result.id]);
  }
  canLeave(): boolean | Promise<boolean> {
    if (this.session.current() && this.deleting()) return false;
    return this.dialog().canLeave(this.dirty());
  }
  async requestDiscard(discard: () => void): Promise<void> {
    if (await this.dialog().confirmDiscard()) discard();
  }
}
