import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ServiceError } from '../common/service-error';
import { OperationResult } from '../operation/operation-result';
import { ReferenceResult } from '../reference/reference-result';
import { IReferenceAnalysisService } from './reference-analysis.service.contract';
import { ReferenceSuggestions } from './reference-suggestions';
import { ReferenceSuggestionReview } from './reference-suggestion-review';

@Injectable()
export class ReferenceAnalysisService implements IReferenceAnalysisService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  current(id: string): Promise<OperationResult | null> {
    return this.call('GET', id, 'analysis');
  }
  suggestions(id: string): Promise<ReferenceSuggestions | null> {
    return this.call('GET', id, 'suggestions');
  }
  request(id: string, revision: number, operationKey: string): Promise<OperationResult> {
    return this.call('POST', id, 'analysis', { revision }, operationKey);
  }
  review(id: string, decision: ReferenceSuggestionReview): Promise<ReferenceResult> {
    return this.call('PUT', id, 'suggestions', decision);
  }
  undo(id: string, revision: number): Promise<ReferenceResult> {
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
        this.http.request<T>(method, '/api/references/' + encodeURIComponent(id) + '/' + path, {
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
