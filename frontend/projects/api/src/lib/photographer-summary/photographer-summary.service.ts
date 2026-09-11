import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ServiceError } from '../common/service-error';
import { OperationResult } from '../operation/operation-result';
import { PhotographerResult } from '../photographer/photographer-metadata';
import { IPhotographerSummaryService } from './photographer-summary.service.contract';
import { PhotographerSuggestions } from './photographer-suggestions';
import { PhotographerSuggestionReview } from './photographer-suggestion-review';

@Injectable()
export class PhotographerSummaryService implements IPhotographerSummaryService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  current(id: string): Promise<OperationResult | null> {
    return this.call('GET', id, 'summary-analysis');
  }
  suggestions(id: string): Promise<PhotographerSuggestions | null> {
    return this.call('GET', id, 'suggestions');
  }
  request(id: string, revision: number, operationKey: string): Promise<OperationResult> {
    return this.call('POST', id, 'summary-analysis', { revision }, operationKey);
  }
  review(id: string, decision: PhotographerSuggestionReview): Promise<PhotographerResult> {
    return this.call('PUT', id, 'suggestions', decision);
  }
  undo(id: string, revision: number): Promise<PhotographerResult> {
    return this.call('POST', id, 'suggestions/undo', { revision });
  }
  private async call<T>(
    method: string,
    id: string,
    path: string,
    body?: object,
    key?: string,
  ): Promise<T> {
    try {
      const headers: Record<string, string> = {};
      if (method !== 'GET') headers['X-CSRF-Token'] = await this.session.getRequestToken();
      if (key) headers['Idempotency-Key'] = key;
      return await firstValueFrom(
        this.http.request<T>(method, '/api/photographers/' + encodeURIComponent(id) + '/' + path, {
          body,
          headers,
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
}
