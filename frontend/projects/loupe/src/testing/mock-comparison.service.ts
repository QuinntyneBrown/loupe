import { Injectable } from '@angular/core';
import { ComparisonResult, IComparisonService, ServiceError } from 'api';

@Injectable()
export class MockComparisonService implements IComparisonService {
  async load(firstId: string, secondId: string): Promise<ComparisonResult> {
    const callback = (
      window as Window & {
        loupePhotographs?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographs;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback('compare', { firstId, secondId });
    if (response.error) throw new ServiceError(response.error);
    return response.data as ComparisonResult;
  }
}
