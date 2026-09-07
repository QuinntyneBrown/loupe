import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PhotographUpload } from 'domain';
import { PhotographResult } from 'api';

@Component({
  selector: 'lp-photograph-upload-page',
  imports: [RouterLink, PhotographUpload],
  templateUrl: './photograph-upload-page.html',
  styleUrl: './photograph-upload-page.css',
})
export class PhotographUploadPage {
  private readonly router = inject(Router);
  saved(photo: PhotographResult): void {
    void this.router.navigate(['/my-work', photo.id]);
  }
}
