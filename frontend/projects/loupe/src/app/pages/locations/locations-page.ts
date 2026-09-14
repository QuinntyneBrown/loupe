import { Component, DOCUMENT, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { LocationCollection } from 'domain';
import { LocationResult, LocationSummary } from 'api';
import { AddLocation } from '../../dialogs/add-location/add-location';
import { DeleteLocation } from '../../dialogs/delete-location/delete-location';

@Component({
  selector: 'lp-locations-page',
  imports: [LocationCollection, AddLocation, DeleteLocation],
  templateUrl: './locations-page.html',
  styleUrl: './locations-page.css',
})
export class LocationsPage {
  readonly adding = signal(false);
  readonly deleting = signal<LocationSummary | null>(null);
  readonly notice = signal(
    inject(Router).currentNavigation()?.extras.state?.['locationDeleted']
      ? 'Location deleted.'
      : '',
  );
  private readonly document = inject(DOCUMENT);
  private readonly collection = viewChild.required(LocationCollection);
  private readonly dialog = viewChild(AddLocation);
  private trigger: Element | null = null;
  openAdd(): void {
    this.trigger = this.document.activeElement;
    this.adding.set(true);
  }
  added(item: LocationResult): void {
    this.adding.set(false);
    this.collection().add(item);
    this.notice.set(`“${item.name}” saved.`);
    this.restoreFocus();
  }
  close(): void {
    this.adding.set(false);
    this.restoreFocus();
  }
  canLeave(): boolean | Promise<boolean> {
    return this.dialog()?.canLeave() ?? true;
  }
  requestDelete(item: LocationSummary): void {
    this.trigger = this.document.activeElement;
    this.deleting.set(item);
  }
  closeDelete(): void {
    this.deleting.set(null);
    this.restoreFocus();
  }
  deleted(): void {
    const item = this.deleting();
    this.deleting.set(null);
    if (item) this.collection().remove(item.id);
    this.notice.set('Location deleted.');
    this.trigger = null;
    this.restoreFocus();
  }
  private restoreFocus(): void {
    const trigger = this.trigger;
    this.trigger = null;
    if (trigger instanceof HTMLElement && trigger.isConnected) trigger.focus();
    else this.collection().focusAdd();
  }
}
