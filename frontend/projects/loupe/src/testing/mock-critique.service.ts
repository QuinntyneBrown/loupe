import { Injectable } from '@angular/core';
import { ICritiqueService, OperationResult, SavedCritique, ServiceError } from 'api';

@Injectable()
export class MockCritiqueService implements ICritiqueService {
  async get(id: string): Promise<SavedCritique | null> {
    return this.read('getCritique', id) as Promise<SavedCritique | null>;
  }
  async getOperation(id: string): Promise<OperationResult | null> {
    return this.read('getCritiqueOperation', id) as Promise<OperationResult | null>;
  }
  private async read(operation: string, id: string): Promise<unknown> {
    const callback = (
      window as Window & {
        loupePhotographs?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographs;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, { id });
    if (response.error) throw new ServiceError(response.error);
    return response.data;
  }
}
