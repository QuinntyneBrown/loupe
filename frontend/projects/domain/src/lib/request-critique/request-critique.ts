import { Component, computed, DestroyRef, inject, input, output, signal } from '@angular/core';
import {
  CRITIQUE_SERVICE,
  CritiqueRequest,
  OperationResult,
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
  readonly admitted = output<OperationResult>();
  readonly started = output<void>();
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly submission = signal<CritiqueRequest | null>(null);
  readonly blocked = computed(() => this.busy() || (this.briefDirty() && !this.submission()));
  private readonly service = inject(CRITIQUE_SERVICE);
  private readonly destroy = inject(DestroyRef);

  async request(): Promise<void> {
    if (this.blocked()) return;
    const photo = this.photograph();
    const submission = this.submission() ?? {
      revision: photo.revision,
      regenerate: false,
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
}
