import { Component, inject, input, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { LocationImage, LocationResult } from 'api';
import { LocationDetailPanel } from 'domain';
import { EditLocation } from '../../dialogs/edit-location/edit-location';
import { DeleteLocation } from '../../dialogs/delete-location/delete-location';
import { AddLocationImages } from '../../dialogs/add-location-images/add-location-images';
import { RemoveLocationImage } from '../../dialogs/remove-location-image/remove-location-image';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-location-detail-page',
  imports: [
    RouterLink,
    LocationDetailPanel,
    EditLocation,
    DeleteLocation,
    AddLocationImages,
    RemoveLocationImage,
    UnsavedChanges,
  ],
  templateUrl: './location-detail-page.html',
  styleUrl: './location-detail-page.css',
})
export class LocationDetailPage {
  readonly id = input.required<string>();
  readonly editing = signal<LocationResult | null>(null);
  readonly deleting = signal<LocationResult | null>(null);
  readonly addingImages = signal<LocationResult | null>(null);
  readonly removingImage = signal<{ location: LocationResult; image: LocationImage } | null>(null);
  readonly refreshable = signal(false);
  readonly notice = signal('');
  private readonly router = inject(Router);
  readonly detail = viewChild(LocationDetailPanel);
  readonly editDialog = viewChild(EditLocation);
  readonly deleteDialog = viewChild(DeleteLocation);
  readonly imagesDialog = viewChild(AddLocationImages);
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
  imageSaved(item: LocationResult): void {
    this.addingImages.set(item);
    this.detail()?.location.set(item);
  }
  closeImages(): void {
    this.addingImages.set(null);
    this.detail()?.focusGallery();
  }
  canceledUploads(): void {
    this.addingImages.set(null);
    this.refreshable.set(true);
    this.notice.set('Uploads stopped. A file may already have been saved. Refresh to check.');
    this.detail()?.focusGallery();
  }
  refresh(): void {
    this.notice.set('');
    this.refreshable.set(false);
    this.detail()?.retry();
  }
  imageRemoved(item: LocationResult): void {
    this.removingImage.set(null);
    this.detail()?.location.set(item);
    this.detail()?.focusGallery();
  }
  closeRemove(): void {
    this.removingImage.set(null);
    this.detail()?.focusGallery();
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
  readonly dirty = () =>
    !this.deleted &&
    !!(this.detail()?.dirty() || this.deleteDialog()?.busy() || this.imagesDialog()?.uploading());
  async canLeave(): Promise<boolean> {
    if (this.deleted) return true;
    const dialog = this.editDialog();
    if (dialog && !(await dialog.canLeave())) return false;
    const leave = await this.unsaved().canLeave(this.dirty());
    if (leave && this.imagesDialog()?.uploading()) this.imagesDialog()?.cancelRemaining();
    return leave;
  }
}
