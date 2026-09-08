import { Component, DOCUMENT, ElementRef, inject, signal, viewChild } from '@angular/core';
import { PhotographCollection } from 'domain';
import { RouterLink } from '@angular/router';
import { PhotographUploadDialog } from '../../dialogs/photograph-upload/photograph-upload-dialog';
import { UploadCompletion } from '../../dialogs/photograph-upload/upload-completion';

@Component({
  selector: 'lp-my-work',
  imports: [PhotographCollection, RouterLink, PhotographUploadDialog],
  templateUrl: './my-work.html',
  styleUrl: './my-work.css',
})
export class MyWork {
  readonly uploading = signal(false);
  readonly notice = signal('');
  readonly savedId = signal<string | null>(null);
  private readonly document = inject(DOCUMENT);
  private readonly uploadButton = viewChild.required<ElementRef<HTMLButtonElement>>('uploadButton');
  completed(result: UploadCompletion): void {
    this.uploading.set(false);
    this.savedId.set(result.photograph.id);
    this.notice.set(result.critique === 'requested' ? 'Uploaded. Critique requested.' : result.critique === 'unconfirmed' ? 'Photograph uploaded. Critique admission was not confirmed.' : 'Photograph uploaded.');
    const trigger = this.document.activeElement;
    void this.collection().refresh().then(() => {
      if (!trigger?.isConnected) this.uploadButton().nativeElement.focus();
    });
  }
  dismissNotice(): void { this.notice.set(''); this.savedId.set(null); this.uploadButton().nativeElement.focus(); }
  private readonly collection = viewChild.required(PhotographCollection);
  canceledTransfer(): void {
    this.savedId.set(null);
    this.notice.set('Transfer stopped. The photograph may already have been saved. Check My Work before uploading again.');
    void this.collection().refresh();
  }
  private readonly upload = viewChild(PhotographUploadDialog);
  canLeave(): boolean | Promise<boolean> { return this.upload()?.canLeave() ?? true; }
}
