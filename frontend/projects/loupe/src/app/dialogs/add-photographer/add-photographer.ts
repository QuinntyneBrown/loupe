import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  PHOTOGRAPHER_DRAFT_SERVICE,
  PhotographerDraftResult,
  PhotographerResult,
  SavePhotographerDraftInput,
  ServiceError,
} from 'api';

@Component({
  selector: 'lp-add-photographer',
  imports: [FormsModule, RouterLink],
  templateUrl: './add-photographer.html',
  styleUrl: './add-photographer.css',
})
export class AddPhotographer {
  private readonly service = inject(PHOTOGRAPHER_DRAFT_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  readonly closed = output<boolean>();
  readonly saved = output<PhotographerResult>();
  readonly phase = signal<'form' | 'reading' | 'preview' | 'fallback' | 'duplicate'>('form');
  readonly draft = signal<PhotographerDraftResult | null>(null);
  readonly url = signal('');
  readonly name = signal('');
  readonly description = signal('');
  readonly notes = signal('');
  readonly tags = signal<string[]>([]);
  readonly tag = signal('');
  readonly saving = signal(false);
  readonly closing = signal(false);
  readonly error = signal('');
  readonly conflict = signal(false);
  readonly saveAttempt = signal<SavePhotographerDraftInput | null>(null);
  readonly locked = computed(() => this.saving() || this.closing() || !!this.saveAttempt());
  readonly dirty = computed(
    () =>
      this.phase() !== 'duplicate' &&
      !!(this.url().trim() || this.name().trim() || this.draft() || this.notes().trim()),
  );
  readonly title = computed(() =>
    this.phase() === 'reading'
      ? 'Reading the page…'
      : this.phase() === 'fallback'
        ? "Couldn't read that site"
        : this.phase() === 'duplicate'
          ? 'Already bookmarked'
          : 'Add photographer',
  );
  readonly host = computed(() => {
    try {
      return new URL(this.url()).hostname;
    } catch {
      return this.url();
    }
  });
  readonly fallbackReason = computed(() =>
    this.draft()?.failureCode === 'robots_disallowed'
      ? `${this.host()} blocks automated reading.`
      : this.draft()?.failureCode === 'source_access_denied'
        ? 'This page requires access Loupe does not have.'
        : this.draft()?.failureCode === 'integration_not_configured'
          ? 'Page reading is not configured.'
          : "This page couldn't be read.",
  );
  private readKey = crypto.randomUUID();
  private saveKey = crypto.randomUUID();
  private generation = 0;
  private readInput: { url: string; name: string | null } | null = null;
  private pendingRead: Promise<PhotographerDraftResult> | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private retryAction: 'read' | 'save' | 'cancel' = 'read';
  private descriptionEdited = false;
  private tagsEdited = false;
  constructor() {
    afterNextRender(() => this.modal().nativeElement.showModal());
    this.destroy.onDestroy(() => {
      this.generation++;
      clearTimeout(this.timer);
      this.modal().nativeElement.close();
    });
  }
  changeDescription(value: string): void {
    this.descriptionEdited = true;
    this.description.set(value);
  }
  addTag(event: Event): void {
    event.preventDefault();
    const name = this.tag().trim().normalize('NFC');
    if (!name) return;
    if (Array.from(name).length > 50 || this.tags().length >= 50) {
      this.error.set('Use up to 50 tags, with 50 characters or fewer per tag.');
      return;
    }
    if (this.tags().some((tag) => tag.toUpperCase() === name.toUpperCase())) {
      this.error.set('This tag is already included.');
      return;
    }
    this.tagsEdited = true;
    this.tags.update((tags) => [...tags, name]);
    this.tag.set('');
    this.error.set('');
  }
  removeTag(name: string): void {
    this.tagsEdited = true;
    this.tags.update((tags) => tags.filter((tag) => tag !== name));
  }
  async read(): Promise<void> {
    if (this.closing()) return;
    if (this.phase() === 'form') {
      if (!this.validUrl()) return;
      if (Array.from(this.name().trim()).length > 200) {
        this.error.set('Use 200 characters or fewer for the name.');
        return;
      }
      this.readInput = { url: this.url().trim(), name: this.name().trim() || null };
      this.readKey = crypto.randomUUID();
    }
    const input = this.readInput;
    if (!input) return;
    this.error.set('');
    this.retryAction = 'read';
    this.phase.set('reading');
    const generation = ++this.generation;
    try {
      const existing = this.draft();
      this.pendingRead = existing
        ? this.service.get(existing.id)
        : this.service.import(input.url, input.name, this.readKey);
      const result = await this.pendingRead;
      if (this.active(generation)) this.consume(result, generation);
    } catch {
      if (this.active(generation))
        this.error.set("Couldn't read the page. Try again; your details are kept.");
    }
  }
  private consume(result: PhotographerDraftResult, generation: number): void {
    this.draft.set(result);
    if (result.committedPhotographerId) {
      this.name.set(result.name || this.name());
      this.phase.set('duplicate');
      this.focusHeading();
      return;
    }
    if (result.import?.status === 'Queued' || result.import?.status === 'Running') {
      this.phase.set('reading');
      this.timer = setTimeout(() => void this.poll(result.id, generation), 1000);
      return;
    }
    if (!this.name()) this.name.set(result.name || '');
    if (!this.descriptionEdited) this.description.set(result.description || '');
    if (!this.tagsEdited) this.tags.set(result.tags);
    this.phase.set(result.failureCode ? 'fallback' : 'preview');
    this.focusHeading();
  }
  private async poll(id: string, generation: number): Promise<void> {
    try {
      const result = await this.service.get(id);
      if (this.active(generation)) this.consume(result, generation);
    } catch {
      if (this.active(generation)) {
        this.retryAction = 'read';
        this.error.set("Couldn't check the page. Try again; your details are kept.");
      }
    }
  }
  private active(generation: number): boolean {
    return !this.destroy.destroyed && generation === this.generation;
  }
  private focusHeading(): void {
    afterNextRender(
      () => {
        if (!this.destroy.destroyed) this.heading()?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  private validUrl(): boolean {
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
      return true;
    } catch {
      this.error.set(
        'Enter a public HTTP or HTTPS website URL without credentials or a custom port.',
      );
      return false;
    }
  }
  async save(): Promise<void> {
    const draft = this.draft();
    if (!draft || this.saving() || this.closing()) return;
    const retrying = !!this.saveAttempt();
    if (!this.saveAttempt()) {
      if (!this.name().trim() || Array.from(this.name().trim()).length > 200) {
        this.error.set('Enter a name using 200 characters or fewer.');
        return;
      }
      if (!this.validUrl()) return;
      if (Array.from(this.description()).length > 4000 || Array.from(this.notes()).length > 10000) {
        this.error.set('Use 4,000 characters or fewer for the description and 10,000 for notes.');
        return;
      }
      if (this.tag().trim()) {
        this.error.set('Press Enter to add the last tag, or clear it before saving.');
        return;
      }
      this.saveAttempt.set({
        revision: draft.revision,
        name: this.name().trim(),
        portfolioUrl: this.url().trim(),
        summary: this.description().trim() || null,
        notes: this.notes().trim() || null,
        tags: this.tags().map((name) => ({ name, category: null })),
      });
      this.saveKey = crypto.randomUUID();
    }
    this.saving.set(true);
    this.error.set('');
    this.retryAction = 'save';
    try {
      const result = await this.service.save(draft.id, this.saveAttempt()!, this.saveKey);
      if (this.destroy.destroyed) return;
      this.saveAttempt.set(null);
      if (result.alreadySaved && !retrying) {
        this.draft.set({ ...draft, committedPhotographerId: result.photographer.id });
        this.name.set(result.photographer.name);
        this.phase.set('duplicate');
        this.focusHeading();
      } else this.saved.emit(result.photographer);
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : '';
      if (code === 'revision_conflict') {
        this.saveAttempt.set(null);
        this.conflict.set(true);
        this.error.set('The page preview changed. Review the latest preview before adding.');
      } else if (code === 'invalid_request') {
        this.saveAttempt.set(null);
        this.error.set('Check the name, website, and field lengths. Nothing was added.');
      } else
        this.error.set(
          "Couldn't add this photographer. Your details are kept. Try again to check the same save.",
        );
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
  async reviewLatest(): Promise<void> {
    const draft = this.draft();
    if (!draft) return;
    try {
      const latest = await this.service.get(draft.id);
      if (this.destroy.destroyed) return;
      this.draft.set(latest);
      this.conflict.set(false);
      this.error.set('Latest page loaded. Review your edits, then add again.');
    } catch {
      this.error.set("Couldn't reload the preview. Your edits are kept.");
    }
  }
  retry(): Promise<unknown> {
    return this.retryAction === 'save'
      ? this.save()
      : this.retryAction === 'cancel'
        ? this.cancel()
        : this.read();
  }
  async cancel(back = false): Promise<boolean> {
    if (this.saving() || this.closing()) return false;
    this.closing.set(true);
    this.error.set('');
    this.retryAction = 'cancel';
    ++this.generation;
    clearTimeout(this.timer);
    const uncertainSave = !!this.saveAttempt();
    try {
      let draft = this.draft();
      if (!draft && this.pendingRead) {
        try {
          draft = await this.pendingRead;
        } catch {
          if (this.readInput)
            draft = await this.service.import(
              this.readInput.url,
              this.readInput.name,
              this.readKey,
            );
        }
      }
      if (draft) {
        try {
          await this.service.cancel(draft.id);
        } catch (error) {
          if (!(error instanceof ServiceError && error.code === 'item_unavailable')) throw error;
        }
      }
      if (this.destroy.destroyed) return true;
      this.draft.set(null);
      this.pendingRead = null;
      this.saveAttempt.set(null);
      this.conflict.set(false);
      if (back) {
        this.phase.set('form');
        this.focusHeading();
      } else this.closed.emit(uncertainSave);
      return true;
    } catch {
      if (!this.destroy.destroyed)
        this.error.set("Couldn't close this preview. Try again to cancel the page read.");
      return false;
    } finally {
      if (!this.destroy.destroyed) this.closing.set(false);
    }
  }
  escape(event: Event): void {
    event.preventDefault();
    void this.cancel();
  }
}
