import { Component, signal, viewChild } from '@angular/core';
import { PhotographCollection } from 'domain';
import { RouterLink } from '@angular/router';
import { PhotographUploadDialog } from '../../dialogs/photograph-upload/photograph-upload-dialog';

@Component({
  selector: 'lp-my-work',
  imports: [PhotographCollection, RouterLink, PhotographUploadDialog],
  templateUrl: './my-work.html',
  styleUrl: './my-work.css',
})
export class MyWork {
  readonly uploading = signal(false);
  readonly notice = signal('');
  private readonly collection = viewChild.required(PhotographCollection);
  canceledTransfer(): void {
    this.notice.set('Transfer stopped. The photograph may already have been saved. Check My Work before uploading again.');
    void this.collection().refresh();
  }
  private readonly upload = viewChild(PhotographUploadDialog);
  canLeave(): boolean | Promise<boolean> { return this.upload()?.canLeave() ?? true; }
}
