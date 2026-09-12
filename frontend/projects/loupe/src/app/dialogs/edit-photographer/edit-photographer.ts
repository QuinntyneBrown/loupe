import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PHOTOGRAPHER_SERVICE, PhotographerResult, ServiceError } from 'api';
@Component({
  selector: 'lp-edit-photographer',
  imports: [FormsModule],
  templateUrl: './edit-photographer.html',
  styleUrl: './edit-photographer.css',
})
export class EditPhotographer {
  readonly photographer = input.required<PhotographerResult>();
  readonly saved = output<PhotographerResult>();
  readonly closed = output<void>();
  private readonly service = inject(PHOTOGRAPHER_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  readonly base = signal<PhotographerResult | null>(null);
  readonly name = signal('');
  readonly url = signal('');
  readonly description = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly stale = signal(false);
  readonly retryable = signal(false);
  readonly dirty = computed(
    () =>
      this.busy() ||
      (!!this.base() &&
        (this.name() !== this.base()!.name ||
          this.url() !== this.base()!.portfolioUrl ||
          this.description() !== (this.base()!.summary ?? ''))),
  );
  constructor() {
    afterNextRender(() => {
      const item = this.photographer();
      this.base.set(item);
      this.name.set(item.name);
      this.url.set(item.portfolioUrl);
      this.description.set(item.summary ?? '');
      this.modal().nativeElement.showModal();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (!this.busy()) this.closed.emit();
  }
  async review(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const latest = await this.service.get(this.photographer().id);
      if (!this.destroy.destroyed) {
        this.base.set(latest);
        this.stale.set(false);
        this.retryable.set(false);
      }
    } catch {
      if (!this.destroy.destroyed)
        this.error.set("Couldn't load the latest details. Your edits are kept.");
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async save(): Promise<void> {
    const base = this.base();
    if (!base || this.busy() || this.stale()) return;
    this.retryable.set(false);
    this.error.set('');
    if (
      !this.name().trim() ||
      Array.from(this.name().trim()).length > 200 ||
      Array.from(this.description()).length > 4000
    ) {
      this.error.set('Enter a name up to 200 characters and a description up to 4,000 characters.');
      return;
    }
    try {
      const url = new URL(this.url().trim());
      if (
        !['http:', 'https:'].includes(url.protocol) ||
        url.username ||
        url.password ||
        url.port ||
        Array.from(this.url().trim()).length > 2048
      )
        throw new Error();
    } catch {
      this.error.set('Enter a public HTTP or HTTPS website URL.');
      return;
    }
    this.busy.set(true);
    try {
      const result = await this.service.update(base.id, {
        revision: base.revision,
        name: this.name().trim(),
        portfolioUrl: this.url().trim(),
        summary: this.description().trim() || null,
        notes: base.notes,
        tags: base.tags.map(({ name, category }) => ({ name, category })),
      });
      if (!this.destroy.destroyed) this.saved.emit(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      this.stale.set(code === 'revision_conflict');
      this.retryable.set(
        ![
          'revision_conflict',
          'invalid_request',
          'portfolio_conflict',
          'item_unavailable',
        ].includes(code),
      );
      this.error.set(
        code === 'revision_conflict'
          ? 'This bookmark changed. Review latest before saving your edits.'
          : code === 'portfolio_conflict'
            ? 'This website is already bookmarked. Choose a different website.'
            : code === 'item_unavailable'
              ? 'This bookmark is no longer available.'
              : code === 'invalid_request'
                ? 'Check the website and field lengths. Your edits are kept.'
                : "Couldn't save. Your edits are kept.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
