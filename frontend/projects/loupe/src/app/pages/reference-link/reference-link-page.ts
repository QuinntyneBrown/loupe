import { Component, inject, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ReferenceLinkPanel } from 'domain';
import { ReferenceResult } from 'api';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-reference-link-page',
  imports: [RouterLink, ReferenceLinkPanel, UnsavedChanges],
  templateUrl: './reference-link-page.html',
  styleUrl: './reference-link-page.css',
})
export class ReferenceLinkPage {
  readonly form = viewChild(ReferenceLinkPanel);
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
