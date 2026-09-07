import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CritiqueBrief, PHOTOGRAPH_SERVICE, PhotographResult, ServiceError } from 'api';

@Component({
  selector: 'lp-photograph-brief',
  imports: [FormsModule],
  templateUrl: './photograph-brief.html',
  styleUrl: './photograph-brief.css',
})
export class PhotographBrief {
  readonly photograph = input.required<PhotographResult>();
  readonly saved = output<PhotographResult>();
  readonly discardRequested = output<() => void>();
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly editButton = viewChild<ElementRef<HTMLButtonElement>>('editButton');
  private readonly firstField = viewChild<ElementRef<HTMLTextAreaElement>>('firstField');
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly failed = signal(false);
  readonly conflicted = signal(false);
  readonly reloading = signal(false);
  readonly reloadFailed = signal(false);
  readonly latestBrief = signal<CritiqueBrief | null>(null);
  private readonly savedBrief = signal<CritiqueBrief | null>(null);
  readonly busy = computed(() => this.saving() || this.reloading());
  readonly fields = [
    { key: 'intent', label: 'Intent' },
    { key: 'genre', label: 'Genre' },
    { key: 'experience', label: 'Experience' },
    { key: 'requestedFeedback', label: 'Requested feedback' },
  ] as const;
  readonly draft = signal<CritiqueBrief>({
    intent: null,
    genre: null,
    experience: null,
    requestedFeedback: null,
  });
  private readonly revision = signal(1);
  readonly normalized = computed(() => {
    const value = this.draft();
    const clean = (text: string | null) => text?.replace(/\r\n?/g, '\n').trim() || null;
    return {
      intent: clean(value.intent),
      genre: clean(value.genre),
      experience: value.experience,
      requestedFeedback: clean(value.requestedFeedback),
    };
  });
  readonly errors = computed(() => {
    const value = this.normalized();
    return {
      intent: [...(value.intent ?? '')].length > 2000,
      genre: [...(value.genre ?? '')].length > 100,
      requestedFeedback: [...(value.requestedFeedback ?? '')].length > 2000,
    };
  });
  readonly invalid = computed(() => Object.values(this.errors()).some(Boolean));
  readonly dirty = computed(
    () =>
      this.editing() &&
      this.fields.some((field) => this.normalized()[field.key] !== this.savedBrief()?.[field.key]),
  );

  constructor() {
    effect(() => {
      const photo = this.photograph();
      untracked(() => {
        if (
          this.editing() &&
          this.fields.every((field) => photo.brief[field.key] === this.savedBrief()?.[field.key])
        )
          this.revision.set(photo.revision);
      });
    });
  }

  open(): void {
    this.draft.set({ ...this.photograph().brief });
    this.savedBrief.set(this.photograph().brief);
    this.revision.set(this.photograph().revision);
    this.failed.set(false);
    this.conflicted.set(false);
    this.reloadFailed.set(false);
    this.latestBrief.set(null);
    this.editing.set(true);
    afterNextRender(() => this.firstField()?.nativeElement.focus(), { injector: this.injector });
  }
  close(): void {
    this.editing.set(false);
    afterNextRender(() => this.editButton()?.nativeElement.focus(), { injector: this.injector });
  }
  cancel(): void {
    if (this.dirty()) this.discardRequested.emit(() => this.close());
    else this.close();
  }
  setText(field: 'intent' | 'genre' | 'requestedFeedback', value: string): void {
    this.draft.update((draft) => ({ ...draft, [field]: value || null }));
    this.failed.set(false);
  }
  setExperience(value: CritiqueBrief['experience']): void {
    this.draft.update((draft) => ({ ...draft, experience: value || null }));
    this.failed.set(false);
  }
  async save(): Promise<void> {
    if (this.busy() || this.conflicted() || this.invalid()) return;
    this.saving.set(true);
    this.failed.set(false);
    try {
      const photo = await this.service.updateBrief(
        this.photograph().id,
        this.revision(),
        this.normalized(),
      );
      if (this.destroy.destroyed) return;
      this.saved.emit(photo);
      this.close();
    } catch (error) {
      if (!this.destroy.destroyed) {
        if (error instanceof ServiceError && error.code === 'revision_conflict') {
          this.conflicted.set(true);
          this.latestBrief.set(null);
        } else this.failed.set(true);
      }
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
  async reloadLatest(): Promise<void> {
    if (!this.conflicted() || this.busy()) return;
    this.reloading.set(true);
    this.reloadFailed.set(false);
    try {
      const photo = await this.service.get(this.photograph().id);
      if (this.destroy.destroyed) return;
      this.revision.set(photo.revision);
      this.savedBrief.set(photo.brief);
      this.latestBrief.set(photo.brief);
      this.conflicted.set(false);
      this.saved.emit(photo);
    } catch {
      if (!this.destroy.destroyed) this.reloadFailed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.reloading.set(false);
    }
  }
}
