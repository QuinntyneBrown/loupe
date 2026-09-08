import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PhotographUpload, RequestCritique } from 'domain';
import { OperationResult, PhotographResult } from 'api';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-photograph-upload-page',
  imports: [RouterLink, PhotographUpload, UnsavedChanges, RequestCritique],
  templateUrl: './photograph-upload-page.html',
  styleUrl: './photograph-upload-page.css',
})
export class PhotographUploadPage {
  readonly form = viewChild(PhotographUpload);
  readonly savedPhotograph = signal<PhotographResult | null>(null);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly dialog = viewChild.required(UnsavedChanges);
  private readonly router = inject(Router);
  readonly dirty = () => this.form()?.dirty() ?? false;
  canLeave(): boolean | Promise<boolean> {
    return this.dialog().canLeave(this.form()?.dirty() ?? false);
  }
  saved(photo: PhotographResult): void {
    if (this.form()?.requestCritique()) {
      this.savedPhotograph.set(photo);
      return;
    }
    void this.router.navigate(['/my-work', photo.id]);
  }
  admitted(operation: OperationResult): void {
    if (operation.resourceId === this.savedPhotograph()?.id)
      void this.router.navigate(['/my-work', operation.resourceId]);
  }
  focusSaved(): void {
    this.heading()?.nativeElement.focus();
  }
}
