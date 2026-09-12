import {
  afterNextRender,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LOCATION_SERVICE, LocationImage, LocationResult, ServiceError } from 'api';
import { LocationGallery } from '../location-gallery/location-gallery';
import { LocationTextEditor } from '../location-text/location-text-editor';
import { LocationTags } from '../location-tags/location-tags';
import { ScoutingReportPanel } from '../scouting-report/scouting-report-panel';
import { SearchIndexStatus } from '../search-index-status/search-index-status';

@Component({
  selector: 'lp-location-detail-panel',
  imports: [
    DatePipe,
    RouterLink,
    LocationGallery,
    LocationTextEditor,
    LocationTags,
    ScoutingReportPanel,
    SearchIndexStatus,
  ],
  templateUrl: './location-detail-panel.html',
  styleUrl: './location-detail-panel.css',
})
export class LocationDetailPanel {
  readonly id = input.required<string>();
  readonly editRequested = output<LocationResult>();
  readonly deleteRequested = output<LocationResult>();
  readonly addImagesRequested = output<LocationResult>();
  readonly removeImageRequested = output<{ location: LocationResult; image: LocationImage }>();
  private readonly service = inject(LOCATION_SERVICE);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private readonly actions = viewChild<ElementRef<HTMLElement>>('actions');
  private readonly texts = viewChildren(LocationTextEditor);
  private readonly tags = viewChild(LocationTags);
  private readonly gallery = viewChild(LocationGallery);
  readonly location = signal<LocationResult | null>(null);
  readonly loading = signal(true);
  readonly unavailable = signal(false);
  readonly failed = signal(false);
  readonly attempt = signal(0);
  readonly dirty = computed(
    () => this.texts().some((editor) => editor.dirty() || editor.busy()) || !!this.tags()?.dirty(),
  );
  readonly saving = computed(
    () => this.texts().some((editor) => editor.busy()) || !!this.tags()?.busy(),
  );
  readonly addressLines = computed(() => {
    const item = this.location();
    return item
      ? [
          item.addressLine1,
          item.addressLine2,
          item.locality,
          item.region,
          item.postalCode,
          item.country,
        ].filter((line): line is string => !!line)
      : [];
  });
  readonly place = computed(() => {
    const item = this.location();
    return item ? [item.locality, item.region].filter((part) => part).join(', ') : '';
  });
  readonly reportStatus = computed(() => {
    switch (this.location()?.reportStatus) {
      case 'Queued':
        return { label: 'Queued', modifier: 'lp-status--queued' };
      case 'Running':
        return { label: 'Scouting', modifier: 'lp-status--analyzing' };
      case 'Ready':
        return { label: 'Report ready', modifier: 'lp-status--ready' };
      case 'Outdated':
        return { label: 'Outdated', modifier: 'lp-status--outdated' };
      case 'Failed':
        return { label: 'Failed', modifier: 'lp-status--failed' };
      default:
        return null;
    }
  });
  constructor() {
    let previousId: string | undefined;
    effect((onCleanup) => {
      const id = this.id();
      const focusResult = this.attempt() > 0 && previousId === id;
      previousId = id;
      let active = true;
      onCleanup(() => (active = false));
      this.location.set(null);
      this.loading.set(true);
      this.failed.set(false);
      this.unavailable.set(false);
      void this.service
        .get(id)
        .then((item) => {
          if (active) this.location.set(item);
        })
        .catch((error) => {
          if (!active) return;
          this.failed.set(true);
          this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        })
        .finally(() => {
          if (!active) return;
          this.loading.set(false);
          if (focusResult)
            afterNextRender(
              () => {
                if (active) this.heading()?.nativeElement.focus();
              },
              { injector: this.injector },
            );
        });
    });
  }
  retry(): void {
    this.attempt.update((value) => value + 1);
  }
  focusActions(): void {
    this.actions()?.nativeElement.focus();
  }
  focusGallery(): void {
    this.gallery()?.focus();
  }
  selectImage(id: string): void {
    this.gallery()?.select(id);
    this.gallery()?.focus();
  }
  request(menu: HTMLDetailsElement, item: LocationResult, action: 'edit' | 'delete'): void {
    menu.open = false;
    if (action === 'edit') this.editRequested.emit(item);
    else this.deleteRequested.emit(item);
  }
}
