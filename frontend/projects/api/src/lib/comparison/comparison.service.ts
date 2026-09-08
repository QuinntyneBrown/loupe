import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IComparisonService } from './comparison.service.contract';
import { ComparisonResult } from './comparison-result';
import { ServiceError } from '../common/service-error';

@Injectable()
export class ComparisonService implements IComparisonService {
  private readonly http = inject(HttpClient);
  async load(firstId: string, secondId: string): Promise<ComparisonResult> {
    try {
      return await firstValueFrom(
        this.http.get<ComparisonResult>('/api/comparisons', {
          params: { firstId, secondId },
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
