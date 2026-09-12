import { Component, inject, signal, viewChild } from '@angular/core';
import { PhotographerCollection } from 'domain';
import { PhotographerResult, SESSION_SERVICE } from 'api';
import { AddPhotographer } from '../../dialogs/add-photographer/add-photographer';
import { UnsavedChanges } from '../../dialogs/unsaved-changes/unsaved-changes';
import { Router } from '@angular/router';

@Component({
  selector: 'lp-photographers-page',
  imports: [PhotographerCollection, AddPhotographer, UnsavedChanges],
  templateUrl: './photographers-page.html',
  styleUrl: './photographers-page.css',
})
export class PhotographersPage {
  readonly adding = signal(false);
  readonly notice = signal(
    inject(Router).currentNavigation()?.extras.state?.['photographerDeleted']
      ? 'Photographer deleted. Their references are still in your library.'
      : '',
  );
  readonly dialog = viewChild(AddPhotographer);
  private readonly collection = viewChild.required(PhotographerCollection);
  private readonly unsaved = viewChild.required(UnsavedChanges);
  private readonly session = inject(SESSION_SERVICE);
  readonly dirty = () => this.dialog()?.dirty() ?? false;
  added(item: PhotographerResult): void {
    this.adding.set(false);
    this.collection().add(item);
    this.notice.set(`“${item.name}” bookmarked.`);
  }
  close(refresh: boolean): void {
    this.adding.set(false);
    if (refresh) void this.collection().refresh();
  }
  async canLeave(): Promise<boolean> {
    if (!this.session.current()) return true;
    if (!(await this.unsaved().canLeave(this.dirty()))) return false;
    const dialog = this.dialog();
    return !dialog || dialog.saving() || (await dialog.cancel());
  }
}
