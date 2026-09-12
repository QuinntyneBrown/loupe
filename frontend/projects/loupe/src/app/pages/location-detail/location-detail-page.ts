import { Component, inject, input, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { LocationResult } from 'api';
import { LocationDetailPanel } from 'domain';
import { EditLocation } from '../../dialogs/edit-location/edit-location';
import { DeleteLocation } from '../../dialogs/delete-location/delete-location';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-location-detail-page',
  imports: [RouterLink, LocationDetailPanel, EditLocation, DeleteLocation, UnsavedChanges],
  templateUrl: './location-detail-page.html',
  styleUrl: './location-detail-page.css',
})
export class LocationDetailPage {
  readonly id = input.required<string>();
  readonly editing = signal<LocationResult | null>(null);
  readonly deleting = signal<LocationResult | null>(null);
  readonly notice = signal('');
  private readonly router = inject(Router);
  readonly detail = viewChild(LocationDetailPanel);
  readonly editDialog = viewChild(EditLocation);
  readonly deleteDialog = viewChild(DeleteLocation);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  private deleted = false;
  saved(item: LocationResult): void {
    this.editing.set(null);
    this.detail()?.location.set(item);
    this.notice.set('Changes saved.');
    this.detail()?.focusActions();
  }
  closeEdit(): void {
    this.editing.set(null);
    this.detail()?.focusActions();
  }
  closeDelete(): void {
    this.deleting.set(null);
    this.detail()?.focusActions();
  }
  async deletionComplete(): Promise<void> {
    this.deleted = true;
    this.deleting.set(null);
    await this.router.navigate(['/locations'], { state: { locationDeleted: true } });
  }
  readonly dirty = () => !this.deleted && !!(this.detail()?.dirty() || this.deleteDialog()?.busy());
  async canLeave(): Promise<boolean> {
    if (this.deleted) return true;
    const dialog = this.editDialog();
    if (dialog && !(await dialog.canLeave())) return false;
    return this.unsaved().canLeave(this.dirty());
  }
}
