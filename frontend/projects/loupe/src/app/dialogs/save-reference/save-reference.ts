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
import { RouterLink } from '@angular/router';
import {
  REFERENCE_DRAFT_SERVICE,
  ReferenceDraftResult,
  ReferenceResult,
  ServiceError,
  UploadProgress,
  BOARD_SERVICE,
  BoardResult,
} from 'api';

@Component({
  selector: 'lp-save-reference',
  imports: [FormsModule, RouterLink],
  templateUrl: './save-reference.html',
  styleUrl: './save-reference.css',
})
export class SaveReference {
  readonly boardId = input<string | null>(null);
  readonly boards = signal<BoardResult[]>([]);
  readonly selectedBoards = signal<string[]>([]);
  readonly newBoardName = signal('');
  readonly loadingBoards = signal(true);
  readonly boardError = signal('');
  readonly sourceKind = signal<'link' | 'upload'>('link');
  readonly phase = signal<'form' | 'importing' | 'preview' | 'failed' | 'duplicate'>('form');
  readonly notes = signal('');
  readonly closing = signal(false);
  readonly heading = computed(() =>
    this.phase() === 'importing'
      ? 'Importing…'
      : this.phase() === 'failed'
        ? "Couldn't import this page"
        : this.phase() === 'duplicate'
          ? 'Already saved'
          : 'Save reference',
  );
  readonly sourceHost = computed(() => {
    try {
      return new URL(this.draft()?.sourceUrl || this.source()).host;
    } catch {
      return '';
    }
  });
  readonly closed = output<void>();
  readonly saved = output<ReferenceResult>();
  readonly draft = signal<ReferenceDraftResult | null>(null);
  readonly file = signal<File | null>(null);
  readonly title = signal('');
  readonly photographer = signal('');
  readonly source = signal('');
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly progress = signal<UploadProgress | null>(null);
  private readonly service = inject(REFERENCE_DRAFT_SERVICE);
  private readonly boardService = inject(BOARD_SERVICE);
  private readonly obsoleteDrafts = new Set<string>();
  private readonly destroy = inject(DestroyRef);
  private readonly modal = viewChild.required<ElementRef<HTMLDialogElement>>('modal');
  private uploadKey = crypto.randomUUID();
  private saveKey = crypto.randomUUID();
  private saveFingerprint = '';
  private importKey = crypto.randomUUID();
  private importedSource = '';
  private timer: ReturnType<typeof setTimeout> | undefined;
  constructor() {
    afterNextRender(() => {
      this.modal().nativeElement.showModal();
      this.selectedBoards.set(this.boardId() ? [this.boardId()!] : []);
      void this.loadBoards();
    });
    this.destroy.onDestroy(() => {
      clearTimeout(this.timer);
      this.modal().nativeElement.close();
    });
  }
  async loadBoards(): Promise<void> {
    this.loadingBoards.set(true);
    this.boardError.set('');
    try {
      const boards = await this.boardService.list();
      if (!this.destroy.destroyed) this.boards.set(boards);
    } catch {
      if (!this.destroy.destroyed) this.boardError.set('Boards could not be loaded. Try again.');
    } finally {
      if (!this.destroy.destroyed) this.loadingBoards.set(false);
    }
  }
  toggleBoard(id: string): void {
    this.selectedBoards.update((ids) =>
      ids.includes(id) ? ids.filter((value) => value !== id) : [...ids, id],
    );
  }
  private async discardObsolete(): Promise<void> {
    for (const id of this.obsoleteDrafts) {
      await this.service.cancel(id);
      this.obsoleteDrafts.delete(id);
    }
  }
  async back(): Promise<void> {
    const draft = this.draft();
    if (!draft || this.busy() || this.saving()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.discardObsolete();
      await this.service.cancel(draft.id);
      if (!this.destroy.destroyed) {
        this.draft.set(null);
        this.phase.set('form');
        this.uploadKey = crypto.randomUUID();
        this.importKey = crypto.randomUUID();
      }
    } catch {
      if (!this.destroy.destroyed) this.error.set('The preview could not be discarded. Try again.');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async addImage(event: Event): Promise<void> {
    const previous = this.draft();
    if (!previous || this.busy()) return;
    this.choose(event);
    const file = this.file();
    if (!file) return;
    const title = this.title(),
      photographer = this.photographer();
    this.busy.set(true);
    try {
      const next = await this.service.upload(file, previous.sourceUrl ?? '', this.uploadKey);
      if (this.destroy.destroyed) {
        await this.service.cancel(next.id);
        return;
      }
      this.obsoleteDrafts.add(previous.id);
      this.receive(next);
      this.title.set(title);
      this.photographer.set(photographer);
      this.sourceKind.set('upload');
      await this.discardObsolete();
    } catch {
      if (!this.destroy.destroyed)
        this.error.set(
          'The image could not be added. Your source and notes are still here. Try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  choose(event: Event): void {
    const files = (event.target as HTMLInputElement).files;
    this.error.set('');
    this.file.set(null);
    this.uploadKey = crypto.randomUUID();
    if (!files?.length) return;
    if (files.length !== 1) {
      this.error.set('Choose one image.');
      return;
    }
    const file = files[0];
    if (!file.size || file.size > 25_000_000) {
      this.error.set('Choose an image of 25 MB or less that is not empty.');
      return;
    }
    if (
      !['image/jpeg', 'image/png', 'image/heic', 'image/heif', 'image/webp'].includes(file.type)
    ) {
      this.error.set('Choose a JPEG, PNG, HEIC, or WebP image.');
      return;
    }
    this.file.set(file);
  }
  async preview(): Promise<void> {
    const file = this.file();
    if (this.busy() || (this.sourceKind() === 'upload' && !file)) return;
    const source = this.source().trim();
    if (!source && this.sourceKind() === 'link') {
      this.error.set('Enter a link to the photograph.');
      return;
    }
    if (source) {
      try {
        const url = new URL(source);
        if (
          !['http:', 'https:'].includes(url.protocol) ||
          url.username ||
          url.password ||
          url.port ||
          source.length > 2048
        )
          throw new Error();
      } catch {
        this.error.set('Use an HTTP or HTTPS URL without credentials or a nonstandard port.');
        return;
      }
    }
    this.busy.set(true);
    this.error.set('');
    if (source !== this.importedSource) {
      this.importedSource = source;
      this.importKey = crypto.randomUUID();
      this.uploadKey = crypto.randomUUID();
    }
    if (this.sourceKind() === 'link') this.phase.set('importing');
    try {
      const draft =
        this.sourceKind() === 'link'
          ? await this.service.import(source, this.importKey)
          : await this.service.upload(file!, source, this.uploadKey, (value) =>
              this.progress.set(value),
            );
      if (this.destroy.destroyed) {
        await this.service.cancel(draft.id);
        return;
      }
      this.receive(draft);
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.phase.set('form');
        this.error.set(
          error instanceof ServiceError && error.code === 'file_unavailable'
            ? 'Choose the image again to retry.'
            : 'The preview could not be prepared. Try again.',
        );
      }
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  private receive(draft: ReferenceDraftResult): void {
    if (this.destroy.destroyed || this.closing()) return;
    this.draft.set(draft);
    if (draft.committedReferenceId) {
      this.phase.set('duplicate');
      return;
    }
    if (draft.import && ['Queued', 'Running'].includes(draft.import.status)) {
      this.phase.set('importing');
      this.timer = setTimeout(() => void this.poll(), 500);
      return;
    }
    this.title.set(draft.title);
    this.photographer.set(draft.attribution ?? '');
    this.phase.set(draft.failureCode ? 'failed' : 'preview');
  }
  async poll(): Promise<void> {
    const draft = this.draft();
    if (!draft || this.destroy.destroyed || this.closing()) return;
    this.error.set('');
    try {
      this.receive(await this.service.get(draft.id));
    } catch {
      if (!this.destroy.destroyed && !this.closing())
        this.error.set('Import status could not be loaded. Try again.');
    }
  }
  async cancel(event?: Event): Promise<void> {
    event?.preventDefault();
    if (this.closing() || this.saving()) return;
    const draft = this.draft();
    this.closing.set(true);
    clearTimeout(this.timer);
    this.error.set('');
    try {
      await this.discardObsolete();
      if (draft) await this.service.cancel(draft.id);
      if (!this.destroy.destroyed) {
        this.modal().nativeElement.close();
        this.closed.emit();
      }
    } catch {
      if (!this.destroy.destroyed) this.error.set('The preview could not be discarded. Try again.');
    } finally {
      if (!this.destroy.destroyed) {
        this.closing.set(false);
        if (this.phase() === 'importing' && draft)
          this.timer = setTimeout(() => void this.poll(), 500);
      }
    }
  }
  async save(): Promise<void> {
    const draft = this.draft();
    if (
      !draft ||
      this.saving() ||
      this.busy() ||
      this.loadingBoards() ||
      this.boardError() ||
      !['preview', 'failed'].includes(this.phase())
    )
      return;
    const title = this.title().trim(),
      attribution = this.photographer().trim();
    if (!title || title.length > 200 || attribution.length > 200 || this.notes().length > 10000) {
      this.error.set('Enter a title and use 200 characters or fewer for each field.');
      return;
    }
    const metadata = {
      title,
      attribution: attribution || null,
      sourceUrl: draft.sourceUrl,
      notes: this.notes().trim() || null,
    };
    const newName = this.newBoardName().trim().normalize('NFC');
    if ([...newName].length > 80) {
      this.error.set('Use 80 characters or fewer for the board name.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    try {
      await this.discardObsolete();
      if (newName) {
        const board = await this.boardService.create(newName);
        if (this.destroy.destroyed) return;
        this.boards.update((boards) =>
          [...boards, board].sort((a, b) => a.name.localeCompare(b.name)),
        );
        this.selectedBoards.update((ids) => [...ids, board.id]);
        this.newBoardName.set('');
      }
      const boardIds = [...new Set(this.selectedBoards())].sort();
      const fingerprint = JSON.stringify({ metadata, boardIds });
      if (fingerprint !== this.saveFingerprint) {
        this.saveFingerprint = fingerprint;
        this.saveKey = crypto.randomUUID();
      }
      const result = await this.service.save(
        draft.id,
        draft.revision,
        metadata,
        boardIds,
        this.saveKey,
      );
      if (!this.destroy.destroyed) {
        this.modal().nativeElement.close();
        this.saved.emit(result.reference);
      }
    } catch (error) {
      if (!this.destroy.destroyed)
        this.error.set(
          error instanceof ServiceError && error.code === 'board_name_conflict'
            ? 'A board with this name already exists. Choose it above or use another name.'
            : 'The reference could not be saved. Your preview is still here. Try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.saving.set(false);
    }
  }
}
