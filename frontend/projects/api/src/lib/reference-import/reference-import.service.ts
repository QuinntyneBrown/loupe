import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IReferenceImportService } from './reference-import.service.contract';
import { ReferenceImportRequest } from './reference-import-request';
import { OperationResult } from '../operation/operation-result';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ServiceError } from '../common/service-error';
@Injectable()
export class ReferenceImportService implements IReferenceImportService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  async current(id: string): Promise<OperationResult | null> {
    try {
      return await firstValueFrom(
        this.http.get<OperationResult | null>(
          '/api/references/' + encodeURIComponent(id) + '/imports/operation',
          { timeout: 15000 },
        ),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async request(id: string, request: ReferenceImportRequest): Promise<OperationResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.post<OperationResult>(
          '/api/references/' + encodeURIComponent(id) + '/imports',
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
  private failure(error: unknown): ServiceError {
    return new ServiceError(
      error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
        ? error.error.code
        : 'request_failed',
    );
  }
}
