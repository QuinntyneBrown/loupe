import {
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  LOCATION_SERVICE,
  LocationResult,
  OperationResult,
  SCOUTING_REPORT_SERVICE,
  ServiceError,
} from 'api';
import { CitedImage, ScoutingReportContent } from 'components';

@Component({
  selector: 'lp-scouting-report',
  imports: [DatePipe, ScoutingReportContent],
  templateUrl: './scouting-report-panel.html',
  styleUrl: './scouting-report-panel.css',
})
export class ScoutingReportPanel {
  readonly location = input.required<LocationResult>();
  readonly updated = output<LocationResult>();
  readonly imageSelected = output<string>();
  private readonly service = inject(SCOUTING_REPORT_SERVICE);
  private readonly locations = inject(LOCATION_SERVICE);
  private readonly destroy = inject(DestroyRef);
  readonly operation = signal<OperationResult | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notConfigured = signal(false);
  readonly conflicted = signal(false);
  readonly report = computed(() => this.location().report);
  readonly images = computed<CitedImage[]>(() =>
    this.location().images.map((image, index) => ({
      id: image.id,
      index: index + 1,
      previewUrl: image.previewUrl,
    })),
  );
  readonly active = computed(
    () => this.operation()?.status === 'Queued' || this.operation()?.status === 'Running',
  );
  readonly failed = computed(() => this.operation()?.status === 'Failed');
  readonly outdated = computed(
    () => !!this.report() && this.location().reportStatus === 'Outdated',
  );
  readonly briefChanged = computed(() => {
    const report = this.report();
    return !!report && (report.briefSnapshot ?? '') !== (this.location().scoutingBrief ?? '');
  });
  readonly imageCount = computed(() => this.location().images.length);
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private requestInput: { revision: number; regenerate: boolean; key: string } | null = null;
  constructor() {
    let previous = '';
    effect(() => {
      const item = this.location();
      const key = `${item.id}:${item.reportStatus}:${item.report?.operationId ?? ''}`;
      if (key === previous) return;
      previous = key;
      untracked(() => {
        this.generation++;
        clearTimeout(this.timer);
        void this.load();
      });
    });
    this.destroy.onDestroy(() => {
      this.generation++;
      clearTimeout(this.timer);
    });
  }
  async load(): Promise<void> {
    const generation = ++this.generation;
    const id = this.location().id;
    clearTimeout(this.timer);
    try {
      const operation = await this.service.current(id);
      if (this.destroy.destroyed || generation !== this.generation) return;
      const finished =
        !!operation &&
        (operation.status === 'Succeeded' || operation.status === 'Failed') &&
        this.operation()?.status !== operation.status;
      this.operation.set(operation);
      if (finished) {
        const latest = await this.locations.get(id);
        if (this.destroy.destroyed || generation !== this.generation) return;
        this.updated.emit(latest);
      }
      if (this.active()) this.timer = setTimeout(() => void this.load(), 1000);
    } catch {
      if (!this.destroy.destroyed && generation === this.generation)
        this.timer = setTimeout(() => void this.load(), 1000);
    }
  }
  async request(regenerate: boolean): Promise<void> {
    if (this.busy() || this.active() || this.conflicted() || !this.imageCount()) return;
    this.requestInput ??= {
      revision: this.location().revision,
      regenerate,
      key: crypto.randomUUID(),
    };
    await this.run(() =>
      this.service.request(
        this.location().id,
        this.requestInput!.revision,
        this.requestInput!.regenerate,
        this.requestInput!.key,
      ),
    );
  }
  async retry(): Promise<void> {
    const failed = this.operation();
    if (this.busy() || !failed || failed.status !== 'Failed') return;
    this.requestInput ??= {
      revision: this.location().revision,
      regenerate: true,
      key: crypto.randomUUID(),
    };
    await this.run(() =>
      this.service.retry(failed.id, this.requestInput!.revision, this.requestInput!.key),
    );
  }
  private async run(send: () => Promise<OperationResult>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.notConfigured.set(false);
    try {
      const operation = await send();
      if (this.destroy.destroyed) return;
      this.operation.set(operation);
      this.requestInput = null;
      if (operation.status === 'Succeeded') {
        const latest = await this.locations.get(this.location().id);
        if (this.destroy.destroyed) return;
        this.updated.emit(latest);
      } else await this.load();
    } catch (error) {
      if (this.destroy.destroyed) return;
      const code = error instanceof ServiceError ? error.code : 'request_failed';
      this.notConfigured.set(code === 'integration_not_configured');
      this.conflicted.set(code === 'revision_conflict');
      if (code !== 'integration_not_configured') this.requestInput = null;
      this.error.set(
        code === 'integration_not_configured'
          ? ''
          : code === 'revision_conflict'
            ? 'This location changed. Reload the page before requesting a report.'
            : code === 'analysis_limit'
              ? 'Five analyses are already running. Wait for one to finish and try again.'
              : code === 'invalid_request'
                ? 'Add an image before requesting a scouting report.'
                : code === 'retry_unavailable' || code === 'analysis_inputs_changed'
                  ? 'This job cannot be retried. Request a new report instead.'
                  : "Couldn't request the report. Your saved content is unchanged. Try again.",
      );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  reason(): string {
    const operation = this.operation();
    return operation?.message ?? 'The provider did not complete the report.';
  }
}
