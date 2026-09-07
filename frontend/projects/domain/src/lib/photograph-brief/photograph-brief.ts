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
import { CritiqueBrief, PHOTOGRAPH_SERVICE, PhotographResult } from 'api';

@Component({
  selector: 'lp-photograph-brief',
  imports: [FormsModule],
  templateUrl: './photograph-brief.html',
  styleUrl: './photograph-brief.css',
})
export class PhotographBrief {
  readonly photograph = input.required<PhotographResult>();
  readonly saved = output<PhotographResult>();
  private readonly service = inject(PHOTOGRAPH_SERVICE);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly editButton = viewChild<ElementRef<HTMLButtonElement>>('editButton');
  private readonly firstField = viewChild<ElementRef<HTMLTextAreaElement>>('firstField');
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly failed = signal(false);
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

  open(): void {
    this.draft.set({ ...this.photograph().brief });
    this.revision.set(this.photograph().revision);
    this.failed.set(false);
    this.editing.set(true);
    afterNextRender(() => this.firstField()?.nativeElement.focus(), { injector: this.injector });
  }
  close(): void {
    this.editing.set(false);
    afterNextRender(() => this.editButton()?.nativeElement.focus(), { injector: this.injector });
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
    if (this.saving() || this.invalid()) return;
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
    } catch {
      if (!this.destroy.destroyed) this.failed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
