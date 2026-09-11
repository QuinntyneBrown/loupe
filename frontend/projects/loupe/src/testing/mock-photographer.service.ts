import { Injectable } from '@angular/core';
import { IPhotographerService, PhotographerPage, ServiceError } from 'api';

@Injectable()
export class MockPhotographerService implements IPhotographerService {
  async list(cursor?: string): Promise<PhotographerPage> {
    const callback = (
      window as Window & {
        loupePhotographers?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographers;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback('list', { cursor });
    if (result.error) throw new ServiceError(result.error);
    return result.data as PhotographerPage;
  }
}
