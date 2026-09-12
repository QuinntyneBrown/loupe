import { Component, DOCUMENT, inject, signal, viewChild } from '@angular/core';
import { LocationCollection } from 'domain';
import { LocationResult } from 'api';
import { AddLocation } from '../../dialogs/add-location/add-location';

@Component({
  selector: 'lp-locations-page',
  imports: [LocationCollection, AddLocation],
  templateUrl: './locations-page.html',
  styleUrl: './locations-page.css',
})
export class LocationsPage {
  readonly adding = signal(false);
  readonly notice = signal('');
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
  private restoreFocus(): void {
    const trigger = this.trigger;
    this.trigger = null;
    if (trigger instanceof HTMLElement && trigger.isConnected) trigger.focus();
    else this.collection().focusAdd();
  }
}
