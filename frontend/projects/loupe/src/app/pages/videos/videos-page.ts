import { Component, inject, signal, viewChild } from '@angular/core';
import { VideoCollection } from 'domain';
import { SESSION_SERVICE, VideoResult } from 'api';
import { VideoForm } from '../../dialogs/video-form/video-form';
import { DeleteVideo } from '../../dialogs/delete-video/delete-video';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';

@Component({
  selector: 'lp-videos-page',
  imports: [VideoCollection, VideoForm, DeleteVideo, UnsavedChanges],
  templateUrl: './videos-page.html',
  styleUrl: './videos-page.css',
})
export class VideosPage {
  readonly notice = signal('');
  readonly adding = signal(false);
  readonly editing = signal<VideoResult | null>(null);
  readonly deleting = signal<VideoResult | null>(null);
  readonly form = viewChild(VideoForm);
  private readonly collection = viewChild.required(VideoCollection);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  private readonly session = inject(SESSION_SERVICE);
  readonly dirty = () => this.form()?.dirty() ?? false;
  saved(item: VideoResult): void {
    const added = this.adding();
    this.adding.set(false);
    this.editing.set(null);
    if (added) this.collection().add(item);
    else this.collection().replace(item);
    this.notice.set(`“${item.title}” ${added ? 'saved' : 'updated'}.`);
  }
  closeForm(): void {
    this.adding.set(false);
    this.editing.set(null);
  }
  deleted(item: VideoResult): void {
    this.deleting.set(null);
    this.editing.set(null);
    this.collection().remove(item.id);
    this.notice.set(`“${item.title}” deleted.`);
  }
  canLeave(): Promise<boolean> | boolean {
    if (!this.session.current()) return true;
    return this.unsaved().canLeave(this.dirty());
  }
}
