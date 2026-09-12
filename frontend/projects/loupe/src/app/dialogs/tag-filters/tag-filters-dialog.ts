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
import { REFERENCE_SERVICE, ReferenceTagFacet } from 'api';

@Component({
  selector: 'lp-tag-filters-dialog',
  templateUrl: './tag-filters-dialog.html',
  styleUrl: './tag-filters-dialog.css',
})
export class TagFiltersDialog {
  readonly tags = input.required<ReferenceTagFacet[]>();
  readonly selected = input.required<string[]>();
  readonly boardId = input<string | null>(null);
  readonly applied = output<string[]>();
  readonly closed = output<void>();
  readonly draft = signal<string[]>([]);
  readonly count = signal<number | null>(null);
  readonly failed = signal(false);
  readonly error = signal('');
  readonly choices = computed(() =>
    [...this.selected(), ...this.tags().map((tag) => tag.name)].filter(
      (tag, i, all) => all.findIndex((other) => this.key(other) === this.key(tag)) === i,
    ),
  );
  private readonly service = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private generation = 0;
  constructor() {
    afterNextRender(() => {
      this.draft.set(this.selected());
      this.modal().nativeElement.showModal();
      void this.preview();
    });
    this.destroy.onDestroy(() => this.modal().nativeElement.close());
  }
  private key(name: string): string {
    return name.normalize('NFC').toUpperCase();
  }
  isSelected(name: string): boolean {
    return this.draft().some((tag) => this.key(tag) === this.key(name));
  }
  toggle(name: string): void {
    this.error.set('');
    if (this.isSelected(name))
      this.draft.update((tags) => tags.filter((tag) => this.key(tag) !== this.key(name)));
    else if (this.draft().length < 10) this.draft.update((tags) => [...tags, name]);
    else {
      this.error.set('Choose up to 10 tags.');
      return;
    }
    void this.preview();
  }
  clear(): void {
    this.draft.set([]);
    this.error.set('');
    void this.preview();
  }
  async preview(): Promise<void> {
    const generation = ++this.generation;
    this.count.set(null);
    this.failed.set(false);
    try {
      const page = await this.service.list(undefined, this.boardId() ?? undefined, this.draft());
      if (!this.destroy.destroyed && generation === this.generation)
        this.count.set(page.totalCount);
    } catch {
      if (!this.destroy.destroyed && generation === this.generation) this.failed.set(true);
    }
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  apply(): void {
    if (this.count() === null) return;
    this.modal().nativeElement.close();
    this.applied.emit(this.draft());
  }
}
