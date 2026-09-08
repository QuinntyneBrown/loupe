import { Component, computed, DestroyRef, inject, input, output, signal } from '@angular/core';
import {
  CRITIQUE_SERVICE,
  CritiqueRequest,
  OperationResult,
  PHOTOGRAPH_SERVICE,
  PhotographResult,
  ServiceError,
} from 'api';

@Component({
  selector: 'lp-request-critique',
  templateUrl: './request-critique.html',
  styleUrl: './request-critique.css',
})
export class RequestCritique {
  readonly photograph = input.required<PhotographResult>();
  readonly briefDirty = input(false);
  readonly regenerate = input(false);
  readonly admitted = output<OperationResult>();
  readonly started = output<void>();
  readonly reviewed = output<PhotographResult>();
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly submission = signal<CritiqueRequest | null>(null);
  readonly reviewing = signal(false);
  readonly reviewFailed = signal(false);
  readonly latest = signal<PhotographResult | null>(null);
  readonly cannotRetry = computed(() =>
    ['revision_conflict', 'analysis_active', 'item_unavailable'].includes(this.error() ?? ''),
  );
  readonly blocked = computed(
    () =>
      this.busy() ||
      this.reviewing() ||
      this.cannotRetry() ||
      (this.briefDirty() && !this.submission()),
  );
  readonly message = computed(() => {
    if (this.reviewFailed())
      return 'Latest saved details could not be loaded. Your edits are still here.';
    switch (this.error()) {
      case 'revision_conflict':
        return 'This photograph changed. Review its latest saved brief before requesting.';
      case 'item_unavailable':
        return 'The photograph is unavailable. Your unsaved edits are still here.';
      case 'analysis_active':
        return 'A critique is already active for this photograph. Check its status before requesting again.';
      case 'analysis_limit':
        return 'Too many analyses are active. Wait for one to finish, then retry.';
      case 'integration_not_configured':
        return 'AI critique is not configured. Your photograph is saved and you can keep editing it.';
      default:
        return 'Critique admission was not confirmed. Retry to check the same request.';
    }
  });
  readonly fields = [
    { key: 'intent', label: 'Intent' },
    { key: 'genre', label: 'Genre' },
    { key: 'experience', label: 'Experience' },
    { key: 'requestedFeedback', label: 'Requested feedback' },
  ] as const;
  private readonly service = inject(CRITIQUE_SERVICE);
  private readonly photographs = inject(PHOTOGRAPH_SERVICE);
  private readonly destroy = inject(DestroyRef);

  async request(photo = this.photograph()): Promise<void> {
    if (this.blocked()) return;
    const submission = this.submission() ?? {
      revision: photo.revision,
      regenerate: this.regenerate(),
      operationKey: crypto.randomUUID(),
    };
    this.submission.set(submission);
    this.busy.set(true);
    this.error.set(null);
    this.started.emit();
    try {
      const operation = await this.service.request(photo.id, submission);
      if (!this.destroy.destroyed && photo.id === this.photograph().id)
        this.admitted.emit(operation);
    } catch (error) {
      if (!this.destroy.destroyed && photo.id === this.photograph().id)
        this.error.set(error instanceof ServiceError ? error.code : 'request_failed');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }

  async review(): Promise<void> {
    if (this.busy() || this.reviewing() || this.error() !== 'revision_conflict') return;
    const id = this.photograph().id;
    this.reviewing.set(true);
    this.reviewFailed.set(false);
    this.started.emit();
    try {
      const latest = await this.photographs.get(id);
      if (this.destroy.destroyed || id !== this.photograph().id) return;
      this.latest.set(latest);
      this.error.set(null);
      this.reviewed.emit(latest);
    } catch (error) {
      if (this.destroy.destroyed || id !== this.photograph().id) return;
      if (error instanceof ServiceError && error.code === 'item_unavailable')
        this.error.set(error.code);
      else this.reviewFailed.set(true);
    } finally {
      if (!this.destroy.destroyed) this.reviewing.set(false);
    }
  }

  requestLatest(): void {
    const latest = this.latest();
    if (!latest || this.busy() || this.reviewing() || this.briefDirty()) return;
    this.submission.set(null);
    this.latest.set(null);
    this.error.set(null);
    void this.request(latest);
  }
}
