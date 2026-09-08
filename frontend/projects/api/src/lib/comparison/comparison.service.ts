import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IComparisonService } from './comparison.service.contract';
import { ComparisonAttempt, ComparisonResult } from './comparison-result';
import { ServiceError } from '../common/service-error';
import { PhotographPage } from '../photograph/photograph-page';
import { PHOTOGRAPH_SERVICE } from '../photograph/photograph.service.contract';
import { CRITIQUE_SERVICE } from '../critique/critique.service.contract';

@Injectable()
export class ComparisonService implements IComparisonService {
  private readonly http = inject(HttpClient);
  private readonly photographs = inject(PHOTOGRAPH_SERVICE);
  private readonly critiques = inject(CRITIQUE_SERVICE);
  async readAttempt(id: string): Promise<ComparisonAttempt> {
    const [photograph, critique] = await Promise.all([
      this.photographs.get(id),
      this.critiques.get(id),
    ]);
    if (!critique) throw new ServiceError('invalid_request');
    return { photograph, critique };
  }
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
