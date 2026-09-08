import { Component, inject, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ReferenceImagePanel } from 'domain';
import { ReferenceResult } from 'api';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-reference-upload-page',
  imports: [RouterLink, ReferenceImagePanel, UnsavedChanges],
  templateUrl: './reference-upload-page.html',
  styleUrl: './reference-upload-page.css',
})
export class ReferenceUploadPage {
  readonly form = viewChild(ReferenceImagePanel);
  private readonly dialog = viewChild.required(UnsavedChanges);
  private readonly router = inject(Router);
  readonly dirty = () => this.form()?.dirty() ?? false;
  canLeave(): boolean | Promise<boolean> {
    return this.dialog().canLeave(this.dirty());
  }
  saved(reference: ReferenceResult): void {
    void this.router.navigate(['/inspiration', reference.id]);
  }
}
