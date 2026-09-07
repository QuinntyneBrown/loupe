import { Component, effect, inject, input, signal } from '@angular/core';
import { DELETION_SERVICE, DeletionResult, ServiceError } from 'api';

@Component({
  selector: 'lp-deletion-status',
  templateUrl: './deletion-status.html',
  styleUrl: './deletion-status.css',
})
export class DeletionStatus {
  readonly id = input.required<string>();
  readonly result = signal<DeletionResult | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  private readonly service = inject(DELETION_SERVICE);
  private refresh: (() => Promise<void>) | null = null;
  constructor() {
    effect((onCleanup) => {
      const id = this.id();
      let active = true;
      let pending = false;
      let timer: ReturnType<typeof setTimeout> | undefined;
      this.result.set(null);
      this.error.set(null);
      this.loading.set(true);
      const load = async () => {
        if (pending || !active) return;
        clearTimeout(timer);
        pending = true;
        try {
          const result = await this.service.get(id);
          if (active) {
            this.result.set(result);
            this.error.set(null);
          }
        } catch (error) {
          if (active) {
            const code = error instanceof ServiceError ? error.code : 'request_failed';
            this.error.set(code);
            if (code === 'item_unavailable') this.result.set(null);
          }
        } finally {
          pending = false;
          if (active) {
            this.loading.set(false);
            if (
              this.result()?.status === 'Pending' ||
              ['request_failed', 'service_unavailable', 'unexpected_failure'].includes(
                this.error() ?? '',
              )
            )
              timer = setTimeout(() => void load(), 5000);
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
  retry(): void {
    void this.refresh?.();
  }
}
