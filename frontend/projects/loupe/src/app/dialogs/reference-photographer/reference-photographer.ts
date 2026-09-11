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
import {
  PHOTOGRAPHER_SERVICE,
  REFERENCE_SERVICE,
  PhotographerSummary,
  ReferenceResult,
  ServiceError,
  CreateReferencePhotographer,
} from 'api';
@Component({
  selector: 'lp-reference-photographer',
  imports: [FormsModule],
  templateUrl: './reference-photographer.html',
  styleUrl: './reference-photographer.css',
})
export class ReferencePhotographer {
  readonly reference = input.required<ReferenceResult>();
  readonly closed = output<void>();
  readonly saved = output<ReferenceResult>();
  readonly reviewed = output<ReferenceResult>();
  readonly baseline = signal<ReferenceResult | null>(null);
  readonly selected = signal<ReferenceResult['photographer']>(null);
  readonly newName = signal('');
  readonly newUrl = signal('');
  readonly createAttempt = signal<CreateReferencePhotographer | null>(null);
  readonly query = signal('');
  readonly items = signal<PhotographerSummary[]>([]);
  readonly cursor = signal<string | null>(null);
  readonly total = signal<number | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal('');
  readonly latest = signal<ReferenceResult | null>(null);
  readonly dirty = computed(
    () =>
      this.busy() ||
      !!this.newName().trim() ||
      this.selected()?.id !== this.baseline()?.photographer?.id,
  );
  private readonly photographers = inject(PHOTOGRAPHER_SERVICE);
  private readonly references = inject(REFERENCE_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  constructor() {
    afterNextRender(() => {
      this.baseline.set(this.reference());
      this.selected.set(this.reference().photographer ?? null);
      this.modal().nativeElement.showModal();
      void this.load();
    });
    this.destroy.onDestroy(() => {
      this.generation++;
      clearTimeout(this.timer);
      this.modal().nativeElement.close();
    });
  }
  host(url: string): string {
    try {
      return new URL(url).hostname;
    } catch {
      return url;
    }
  }
  search(value: string): void {
    this.query.set(value);
    clearTimeout(this.timer);
    this.generation++;
    this.items.set([]);
    this.cursor.set(null);
    this.total.set(null);
    this.loading.set(false);
    this.timer = setTimeout(() => void this.load(), 200);
  }
  choose(item: PhotographerSummary): void {
    this.newName.set('');
    this.newUrl.set('');
    this.selected.set(item);
    this.unavailable.set(false);
    if (!this.stale()) this.error.set('');
  }
  enterName(value: string): void {
    this.newName.set(value);
    if (value.trim()) this.selected.set(null);
    this.unavailable.set(false);
    if (!this.stale()) this.error.set('');
  }
  async load(): Promise<void> {
    if (this.loading()) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.loadFailed.set(false);
    try {
      const result = await this.photographers.list(this.cursor() ?? undefined, this.query().trim());
      if (this.destroy.destroyed || generation !== this.generation) return;
      this.items.update((items) => [
        ...items,
        ...result.items.filter((item) => !items.some((existing) => existing.id === item.id)),
      ]);
      this.cursor.set(result.nextCursor);
      this.total.set(result.totalCount);
    } catch {
      if (!this.destroy.destroyed && generation === this.generation) this.loadFailed.set(true);
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
  cancel(event?: Event): void {
    event?.preventDefault();
    if (this.busy()) return;
    this.modal().nativeElement.close();
    this.closed.emit();
  }
  private finish(reference: ReferenceResult): void {
    this.modal().nativeElement.close();
    this.saved.emit(reference);
  }
  async review(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      const latest = await this.references.get(this.reference().id);
      if (this.destroy.destroyed) return;
      this.baseline.set(latest);
      this.createAttempt.set(null);
      this.latest.set(latest);
      this.reviewed.emit(latest);
      this.stale.set(false);
      this.error.set('');
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
        this.error.set(
          this.unavailable()
            ? 'This reference is no longer available.'
            : "Couldn't load the latest reference. Your choice is kept.",
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async save(): Promise<void> {
    const base = this.baseline(),
      selected = this.selected();
    if (
      !base ||
      (!selected && !this.newName().trim()) ||
      this.busy() ||
      this.stale() ||
      this.unavailable()
    )
      return;
    if (this.newName().trim() && !this.createAttempt()) {
      const name = this.newName().trim(),
        portfolioUrl = this.newUrl().trim();
      if (Array.from(name).length > 200 || name.includes('\0')) {
        this.error.set('Enter a name using 200 characters or fewer, without null characters.');
        return;
      }
      try {
        const url = new URL(portfolioUrl);
        if (
          !['http:', 'https:'].includes(url.protocol) ||
          url.username ||
          url.password ||
          url.port ||
          /[\s\\]/u.test(portfolioUrl) ||
          Array.from(portfolioUrl).length > 2048
        )
          throw new Error();
      } catch {
        this.error.set(
          'Enter a public HTTP or HTTPS portfolio URL without credentials or a custom port.',
        );
        return;
      }
      this.createAttempt.set({
        revision: base.revision,
        name,
        portfolioUrl,
        operationKey: crypto.randomUUID(),
      });
    }
    this.busy.set(true);
    this.error.set('');
    try {
      const attempt = this.createAttempt();
      const result = attempt
        ? await this.references.createPhotographer(base.id, attempt)
        : await this.references.setPhotographer(base.id, base.revision, selected!.id);
      if (!this.destroy.destroyed) this.finish(result);
    } catch (error) {
      if (this.destroy.destroyed) return;
      if (error instanceof ServiceError && error.code === 'revision_conflict') {
        try {
          const latest = await this.references.get(base.id);
          if (this.destroy.destroyed) return;
          if (selected && latest.photographer?.id === selected.id) {
            this.finish(latest);
            return;
          }
        } catch {
          if (this.destroy.destroyed) return;
        }
        this.stale.set(true);
      }
      if (error instanceof ServiceError && error.code === 'invalid_request') {
        this.createAttempt.set(null);
        this.error.set('Check the name and public portfolio URL. Nothing was added.');
        return;
      }
      this.unavailable.set(error instanceof ServiceError && error.code === 'item_unavailable');
      this.error.set(
        this.stale()
          ? 'This reference changed. Review its latest details before linking.'
          : this.unavailable()
            ? 'The reference or photographer is no longer available.'
            : "Couldn't link this photographer. Your choice is kept. Try Link again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
