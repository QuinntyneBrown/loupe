import { Component, computed, effect, input, output, signal, inject } from '@angular/core';
import { REFERENCE_SERVICE, ReferenceTagFacet } from 'api';

@Component({
  selector: 'lp-reference-filters',
  templateUrl: './reference-filters.html',
  styleUrl: './reference-filters.css',
})
export class ReferenceFilters {
  readonly boardId = input<string | null>(null);
  readonly selected = input<string[]>([]);
  readonly changed = output<string[]>();
  readonly more = output<ReferenceTagFacet[]>();
  readonly tags = signal<ReferenceTagFacet[]>([]);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly attempt = signal(0);
  readonly error = signal('');
  private readonly service = inject(REFERENCE_SERVICE);
  readonly choices = computed(() =>
    [...this.selected(), ...this.tags().map((tag) => tag.name)].filter(
      (name, index, all) => all.findIndex((other) => this.key(other) === this.key(name)) === index,
    ),
  );
  readonly visible = computed(() => this.choices().slice(0, Math.max(8, this.selected().length)));
  constructor() {
    effect((onCleanup) => {
      const boardId = this.boardId();
      this.attempt();
      let active = true;
      onCleanup(() => (active = false));
      this.loading.set(true);
      this.failed.set(false);
      void this.service
        .tags(boardId ?? undefined)
        .then((tags) => {
          if (active) this.tags.set(tags);
        })
        .catch(() => {
          if (active) this.failed.set(true);
        })
        .finally(() => {
          if (active) this.loading.set(false);
        });
    });
  }
  private key(name: string): string {
    return name.normalize('NFC').toUpperCase();
  }
  retry(): void {
    this.attempt.update((value) => value + 1);
  }
  isSelected(name: string): boolean {
    return this.selected().some((tag) => this.key(tag) === this.key(name));
  }
  toggle(name: string): void {
    this.error.set('');
    if (this.isSelected(name))
      this.changed.emit(this.selected().filter((tag) => this.key(tag) !== this.key(name)));
    else if (this.selected().length < 10) this.changed.emit([...this.selected(), name]);
    else this.error.set('Choose up to 10 tags.');
  }
}
