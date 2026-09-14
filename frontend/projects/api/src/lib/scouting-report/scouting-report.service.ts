import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { OperationResult } from '../operation/operation-result';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { IScoutingReportService } from './scouting-report.service.contract';
import { SavedScoutingReport } from './scouting-report-result';

@Injectable()
export class ScoutingReportService implements IScoutingReportService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  current(locationId: string): Promise<OperationResult | null> {
    return this.read(`/api/locations/${encodeURIComponent(locationId)}/scouting-report/operation`);
  }
  get(locationId: string): Promise<SavedScoutingReport | null> {
    return this.read(`/api/locations/${encodeURIComponent(locationId)}/scouting-report`);
  }
  request(
    locationId: string,
    revision: number,
    regenerate: boolean,
    operationKey: string,
  ): Promise<OperationResult> {
    return this.post(
      `/api/locations/${encodeURIComponent(locationId)}/scouting-report`,
      { revision, regenerate },
      operationKey,
    );
  }
  retry(operationId: string, revision: number, operationKey: string): Promise<OperationResult> {
    return this.post(
      `/api/operations/${encodeURIComponent(operationId)}/retry`,
      { revision },
      operationKey,
    );
  }
  private async read<T>(url: string): Promise<T | null> {
    try {
      // An empty 204 body arrives as null.
      return await firstValueFrom(this.http.get<T | null>(url, { timeout: 15000 }));
    } catch (error) {
      throw ScoutingReportService.failure(error);
    }
  }
  private async post(url: string, body: object, operationKey: string): Promise<OperationResult> {
    try {
      return await firstValueFrom(
        this.http.post<OperationResult>(url, body, {
          headers: {
            'Idempotency-Key': operationKey,
            'X-CSRF-Token': await this.session.getRequestToken(),
          },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw ScoutingReportService.failure(error);
    }
  }
  private static failure(error: unknown): ServiceError {
    const body: unknown = error instanceof HttpErrorResponse ? error.error : null;
    if (!body || typeof body !== 'object') return new ServiceError('request_failed');
    const code = 'code' in body && typeof body.code === 'string' ? body.code : 'request_failed';
    const errors: Record<string, string[]> = {};
    if ('errors' in body && body.errors && typeof body.errors === 'object')
      for (const [field, messages] of Object.entries(body.errors))
        if (Array.isArray(messages) && messages.every((message) => typeof message === 'string'))
          errors[field] = messages;
    return new ServiceError(code, errors);
  }
}
