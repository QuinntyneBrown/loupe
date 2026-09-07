import { Component, inject, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PhotographUpload } from 'domain';
import { PhotographResult } from 'api';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-photograph-upload-page',
  imports: [RouterLink, PhotographUpload, UnsavedChanges],
  templateUrl: './photograph-upload-page.html',
  styleUrl: './photograph-upload-page.css',
})
export class PhotographUploadPage {
  readonly form = viewChild(PhotographUpload);
  private readonly dialog = viewChild.required(UnsavedChanges);
  private readonly router = inject(Router);
  canLeave(): boolean | Promise<boolean> {
    return this.dialog().canLeave(this.form()?.dirty() ?? false);
  }
  saved(photo: PhotographResult): void {
    void this.router.navigate(['/my-work', photo.id]);
  }
}
