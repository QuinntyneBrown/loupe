import { Component, inject, input, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { VideoDetail } from 'domain';
import { SESSION_SERVICE, VideoResult } from 'api';
import { VideoForm } from '../../dialogs/video-form/video-form';
import { DeleteVideo } from '../../dialogs/delete-video/delete-video';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-video-detail-page',
  imports: [RouterLink, VideoDetail, VideoForm, DeleteVideo, UnsavedChanges],
  templateUrl: './video-detail-page.html',
  styleUrl: './video-detail-page.css',
})
export class VideoDetailPage {
  readonly id = input.required<string>();
  readonly editing = signal<VideoResult | null>(null);
  readonly deleting = signal<VideoResult | null>(null);
  readonly notice = signal('');
  private readonly router = inject(Router);
  private readonly session = inject(SESSION_SERVICE);
  private readonly detail = viewChild.required(VideoDetail);
  private readonly form = viewChild(VideoForm);
  private readonly deleteDialog = viewChild(DeleteVideo);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  private deleted = false;
  readonly dirty = () => !this.deleted && !!(this.form()?.dirty() || this.deleteDialog()?.busy());
  canLeave(): boolean | Promise<boolean> {
    return !this.session.current() || this.unsaved().canLeave(this.dirty());
  }
  saved(item: VideoResult): void {
    this.detail().item.set(item);
    this.notice.set(`“${item.title}” updated.`);
    this.closeEdit();
  }
  closeEdit(): void {
    this.editing.set(null);
    this.detail().focusActions();
  }
  closeDelete(): void {
    this.deleting.set(null);
    if (!this.editing()) this.detail().focusActions();
  }
  async deletionComplete(item: VideoResult): Promise<void> {
    this.deleted = true;
    this.deleting.set(null);
    this.editing.set(null);
    await this.router.navigate(['/videos'], { state: { videoDeleted: item.title } });
  }
}
