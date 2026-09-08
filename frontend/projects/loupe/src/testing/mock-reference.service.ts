import { Injectable } from '@angular/core';
import { IReferenceService, ReferencePage, ReferenceResult, ServiceError } from 'api';

@Injectable()
export class MockReferenceService implements IReferenceService {
  list(cursor?: string): Promise<ReferencePage> {
    return this.call('list', { cursor });
  }
  get(id: string): Promise<ReferenceResult> {
    return this.call('get', { id });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeReferences?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupeReferences;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data as T;
  }
}
