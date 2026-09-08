import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ICritiqueService } from './critique.service.contract';
import { SavedCritique } from './critique-result';
import { OperationResult } from '../operation/operation-result';
import { ServiceError } from '../common/service-error';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { CritiqueRequest } from './critique-request';
import { CritiqueRetry } from './critique-retry';

@Injectable()
export class CritiqueService implements ICritiqueService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  async retry(operationId: string, request: CritiqueRetry): Promise<OperationResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.post<OperationResult>(
          `/api/operations/${encodeURIComponent(operationId)}/retry`,
          { revision: request.revision },
          {
            headers: { 'X-CSRF-Token': token, 'Idempotency-Key': request.operationKey },
            timeout: 15000,
          },
        ),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async request(photographId: string, request: CritiqueRequest): Promise<OperationResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.post<OperationResult>(
          `/api/photographs/${encodeURIComponent(photographId)}/critique`,
          { revision: request.revision, regenerate: request.regenerate },
          {
            headers: { 'X-CSRF-Token': token, 'Idempotency-Key': request.operationKey },
            timeout: 15000,
          },
        ),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async get(photographId: string): Promise<SavedCritique | null> {
    return this.read<SavedCritique>(
      `/api/photographs/${encodeURIComponent(photographId)}/critique`,
    );
  }
  async getOperation(photographId: string): Promise<OperationResult | null> {
    return this.read<OperationResult>(
      `/api/photographs/${encodeURIComponent(photographId)}/critique/operation`,
    );
  }
  private async read<T>(url: string): Promise<T | null> {
    try {
      return await firstValueFrom(this.http.get<T | null>(url, { timeout: 15000 }));
    } catch (error) {
      throw this.failure(error);
    }
  }
  private failure(error: unknown): ServiceError {
    return new ServiceError(
      error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
        ? error.error.code
        : 'request_failed',
    );
  }
}
