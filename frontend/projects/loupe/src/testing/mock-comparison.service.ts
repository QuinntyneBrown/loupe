import { Injectable } from '@angular/core';
import {
  ComparisonAttempt,
  ComparisonResult,
  IComparisonService,
  PhotographPage,
  PhotographResult,
  SavedCritique,
  ServiceError,
} from 'api';

@Injectable()
export class MockComparisonService implements IComparisonService {
  async readAttempt(id: string): Promise<ComparisonAttempt> {
    const [photograph, critique] = await Promise.all([
      this.call('get', { id }),
      this.call('getCritique', { id }),
    ]);
    if (!critique) throw new ServiceError('invalid_request');
    return { photograph: photograph as PhotographResult, critique: critique as SavedCritique };
  }
  async eligible(cursor?: string): Promise<PhotographPage> {
    return this.call('eligible', { cursor }) as Promise<PhotographPage>;
  }
  async load(firstId: string, secondId: string): Promise<ComparisonResult> {
    return this.call('compare', { firstId, secondId }) as Promise<ComparisonResult>;
  }
  private async call(operation: string, input: object): Promise<unknown> {
    const callback = (
      window as Window & {
        loupePhotographs?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographs;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data;
  }
}
