import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IComparisonService } from './comparison.service.contract';
import { ComparisonResult } from './comparison-result';
import { ServiceError } from '../common/service-error';
import { PhotographPage } from '../photograph/photograph-page';

@Injectable()
export class ComparisonService implements IComparisonService {
  private readonly http = inject(HttpClient);
  async eligible(cursor?: string): Promise<PhotographPage> {
    return this.read<PhotographPage>('/api/comparisons/eligible', cursor ? { cursor } : {});
  }
  async load(firstId: string, secondId: string): Promise<ComparisonResult> {
    return this.read<ComparisonResult>('/api/comparisons', { firstId, secondId });
  }
  private async read<T>(url: string, params: Record<string, string>): Promise<T> {
    try {
      return await firstValueFrom(
        this.http.get<T>(url, {
          params,
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
