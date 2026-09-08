import { Injectable } from '@angular/core';
import { ComparisonResult, IComparisonService, PhotographPage, ServiceError } from 'api';

@Injectable()
export class MockComparisonService implements IComparisonService {
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
