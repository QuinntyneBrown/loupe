import { Injectable } from '@angular/core';
import { DeletionResult, IDeletionService, ServiceError } from 'api';

@Injectable()
export class MockDeletionService implements IDeletionService {
  deletePhotograph(id: string, revision: number): Promise<DeletionResult> {
    return this.request('deletePhotograph', { id, revision });
  }
  get(id: string): Promise<DeletionResult> {
    return this.request('getDeletion', { id });
  }
  private async request(operation: string, input: object): Promise<DeletionResult> {
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
    return response.data as DeletionResult;
  }
}
