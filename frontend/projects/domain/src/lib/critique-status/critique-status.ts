import { Component, effect, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { CRITIQUE_SERVICE, OperationResult, ServiceError } from 'api';

@Component({
  selector: 'lp-critique-status',
  imports: [DatePipe],
  templateUrl: './critique-status.html',
  styleUrl: './critique-status.css',
})
export class CritiqueStatus {
  readonly id = input.required<string>();
  readonly completed = output<string>();
  readonly operation = signal<OperationResult | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  private readonly service = inject(CRITIQUE_SERVICE);
  private refresh: (() => Promise<void>) | null = null;

  constructor() {
    effect((onCleanup) => {
      const id = this.id();
      let active = true;
      let pending = false;
      let completedId: string | null = null;
      let timer: ReturnType<typeof setTimeout> | undefined;
      this.operation.set(null);
      this.error.set(null);
      this.loading.set(true);
      const load = async () => {
        if (pending || !active) return;
        clearTimeout(timer);
        pending = true;
        try {
          const result = await this.service.getOperation(id);
          if (active) {
            this.operation.set(result);
            this.error.set(null);
            if (result?.status === 'Succeeded' && result.id !== completedId) {
              completedId = result.id;
              this.completed.emit(result.id);
            }
          }
        } catch (error) {
          if (active) {
            const code = error instanceof ServiceError ? error.code : 'request_failed';
            this.error.set(code);
            if (code === 'item_unavailable') this.operation.set(null);
          }
        } finally {
          pending = false;
          if (active) {
            this.loading.set(false);
            if (
              this.error() !== 'item_unavailable' &&
              (this.operation()?.status === 'Queued' ||
                this.operation()?.status === 'Running' ||
                ['request_failed', 'service_unavailable', 'unexpected_failure'].includes(
                  this.error() ?? '',
                ))
            )
              timer = setTimeout(() => void load(), 4000);
          }
        }
      };
      this.refresh = load;
      void load();
      onCleanup(() => {
        active = false;
        clearTimeout(timer);
        this.refresh = null;
      });
    });
  }
  check(): void {
    void this.refresh?.();
  }
}
