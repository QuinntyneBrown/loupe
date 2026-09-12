import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PHOTOGRAPHER_SERVICE, PhotographerResult, ServiceError } from 'api';
@Component({
  selector: 'lp-photographer-tags',
  imports: [FormsModule],
  templateUrl: './photographer-tags.html',
  styleUrl: './photographer-tags.css',
})
export class PhotographerTags {
  readonly photographer = input.required<PhotographerResult>();
  readonly saved = output<PhotographerResult>();
  readonly draft = signal('');
  readonly busy = signal(false);
  readonly conflict = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  readonly action = signal<{
    kind: 'add' | 'remove';
    name: string;
    base: PhotographerResult;
  } | null>(null);
  readonly dirty = computed(() => !!this.draft().trim() || !!this.action() || this.busy());
  private readonly service = inject(PHOTOGRAPHER_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private key(value: string): string {
    return value.trim().normalize('NFC').toUpperCase();
  }
  add(event: Event): void {
    event.preventDefault();
    if (this.busy() || this.action()) return;
    const name = this.draft().trim().normalize('NFC');
    if (!name) return;
    const item = this.photographer();
    if (Array.from(name).length > 50 || item.tags.length >= 50) {
      this.error.set('Use up to 50 tags, with 50 characters or fewer per tag.');
      return;
    }
    if (item.tags.some((tag) => this.key(tag.name) === this.key(name))) {
      this.error.set('This tag is already included.');
      return;
    }
    this.action.set({ kind: 'add', name, base: item });
    void this.retry();
  }
  remove(name: string): void {
    if (this.busy() || this.action()) return;
    this.action.set({ kind: 'remove', name, base: this.photographer() });
    void this.retry();
  }
  cancel(): void {
    if (this.busy()) return;
    this.action.set(null);
    this.conflict.set(false);
    this.unavailable.set(false);
    this.error.set('');
    this.focus();
  }
  private focus(): void {
    afterNextRender(
      () => {
        if (!this.destroy.destroyed) this.field().nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  async review(): Promise<void> {
    const action = this.action();
    if (!action || this.busy()) return;
    this.busy.set(true);
    try {
      const latest = await this.service.get(action.base.id);
      if (this.destroy.destroyed) return;
      this.action.set({ ...action, base: latest });
      this.saved.emit(latest);
      this.conflict.set(false);
      this.error.set('Latest tags loaded. Review them, then retry your change.');
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.unavailable()
            ? 'This bookmark is no longer available.'
            : "Couldn't load the latest tags. Try again.",
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async retry(): Promise<void> {
    const action = this.action();
    if (!action || this.busy() || this.conflict() || this.unavailable()) return;
    const base = action.base;
    const tags = base.tags.map(({ name, category }) => ({ name, category }));
    const index = tags.findIndex((tag) => this.key(tag.name) === this.key(action.name));
    if (action.kind === 'add' && index < 0) {
      if (tags.length >= 50) {
        this.error.set(
          'This bookmark already has 50 tags. Cancel this change and remove a tag first.',
        );
        return;
      }
      tags.push({ name: action.name, category: null });
    } else if (action.kind === 'remove' && index >= 0) tags.splice(index, 1);
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.service.update(base.id, {
        revision: base.revision,
        name: base.name,
        portfolioUrl: base.portfolioUrl,
        summary: base.summary,
        notes: base.notes,
        tags,
      });
      if (this.destroy.destroyed) return;
      this.action.set(null);
      if (action.kind === 'add') this.draft.set('');
      this.saved.emit(result);
      this.focus();
    } catch (error) {
      if (this.destroy.destroyed) return;
      this.conflict.set(error instanceof ServiceError && error.code === 'revision_conflict');
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.conflict()
          ? 'This bookmark changed. Review latest tags before retrying.'
          : this.unavailable()
            ? 'This bookmark is no longer available.'
            : "Couldn't save this tag change. Retry or cancel; your tags are kept.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
