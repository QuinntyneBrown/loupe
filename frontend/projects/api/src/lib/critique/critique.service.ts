import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ICritiqueService } from './critique.service.contract';
import { SavedCritique } from './critique-result';
import { OperationResult } from '../operation/operation-result';
import { ServiceError } from '../common/service-error';

@Injectable()
export class CritiqueService implements ICritiqueService {
  private readonly http = inject(HttpClient);
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
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
}
