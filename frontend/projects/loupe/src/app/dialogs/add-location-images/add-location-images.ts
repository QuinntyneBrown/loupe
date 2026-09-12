import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { LocationResult } from 'api';
import { LocationImageUpload } from 'domain';

@Component({
  selector: 'lp-add-location-images',
  imports: [LocationImageUpload],
  templateUrl: './add-location-images.html',
  styleUrl: './add-location-images.css',
})
export class AddLocationImages {
  readonly location = input.required<LocationResult>();
  readonly saved = output<LocationResult>();
  readonly closed = output<void>();
  readonly canceled = output<void>();
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  readonly upload = viewChild.required(LocationImageUpload);
  readonly title = computed(() => {
    const queue = this.upload();
    if (!queue.started()) return 'Add images';
    const count = queue.items().length;
    return queue.uploading()
      ? `Uploading ${count} ${count === 1 ? 'image' : 'images'}…`
      : `${queue.savedCount()} of ${count} ${count === 1 ? 'image' : 'images'} added`;
  });
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  uploading(): boolean {
    return this.upload().uploading();
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (this.uploading()) {
      this.cancelRemaining();
      return;
    }
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  cancelRemaining(): void {
    this.upload().cancelRemaining();
    this.modal().nativeElement.close();
    this.canceled.emit();
  }
  done(): void {
    if (this.uploading()) return;
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  backdrop(event: MouseEvent): void {
    if (event.target !== this.modal().nativeElement) return;
    const bounds = this.modal().nativeElement.getBoundingClientRect();
    if (
      event.clientX < bounds.left ||
      event.clientX > bounds.right ||
      event.clientY < bounds.top ||
      event.clientY > bounds.bottom
    )
      this.cancel();
  }
}
