import { Injectable } from '@angular/core';
import { DeletionResult, IDeletionService, ServiceError } from 'api';

@Injectable()
export class MockDeletionService implements IDeletionService {
  deletePhotographer(id: string, revision: number): Promise<DeletionResult> {
    return this.request('deletePhotographer', { id, revision }, 'loupePhotographers');
  }
  deleteReference(id: string, revision: number): Promise<DeletionResult> {
    return this.request('deleteReference', { id, revision }, 'loupeReferences');
  }
  deletePhotograph(id: string, revision: number): Promise<DeletionResult> {
    return this.request('deletePhotograph', { id, revision });
  }
  deleteLocation(id: string, revision: number): Promise<DeletionResult> {
    return this.request('deleteLocation', { id, revision }, 'loupeLocations');
  }
  get(id: string): Promise<DeletionResult> {
    return this.request('getDeletion', { id });
  }
  private async request(
    operation: string,
    input: object,
    bridge:
      | 'loupeReferences'
      | 'loupePhotographs'
      | 'loupePhotographers'
      | 'loupeLocations' = 'loupePhotographs',
  ): Promise<DeletionResult> {
    const callback = (
      window as Window & {
        [key in 'loupeReferences' | 'loupePhotographs' | 'loupePhotographers' | 'loupeLocations']?:
          | undefined
          | ((operation: string, input: object) => Promise<{ data?: unknown; error?: string }>);
      }
    )[bridge];
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data as DeletionResult;
  }
}
