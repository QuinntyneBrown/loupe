import { Injectable } from '@angular/core';
import { ICritiqueService, SavedCritique, ServiceError } from 'api';

@Injectable()
export class MockCritiqueService implements ICritiqueService {
  async get(id: string): Promise<SavedCritique | null> {
    const callback = (
      window as Window & {
        loupePhotographs?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographs;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback('getCritique', { id });
    if (response.error) throw new ServiceError(response.error);
    return response.data as SavedCritique | null;
  }
}
