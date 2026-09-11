import { Injectable } from '@angular/core';
import {
  IPhotographerService,
  PhotographerPage,
  PhotographerResult,
  PhotographerMetadata,
  PhotographerReferencePage,
  ServiceError,
} from 'api';

@Injectable()
export class MockPhotographerService implements IPhotographerService {
  update(
    id: string,
    input: PhotographerMetadata & { revision: number },
  ): Promise<PhotographerResult> {
    return this.call('update', { id, ...input });
  }
  async list(cursor?: string): Promise<PhotographerPage> {
    return this.call('list', { cursor });
  }
  get(id: string): Promise<PhotographerResult> {
    return this.call('get', { id });
  }
  references(id: string, cursor?: string): Promise<PhotographerReferencePage> {
    return this.call('references', { id, cursor });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupePhotographers?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographers;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error);
    return result.data as T;
  }
}
