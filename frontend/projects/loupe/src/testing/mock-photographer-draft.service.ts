import { Injectable } from '@angular/core';
import {
  IPhotographerDraftService,
  PhotographerDraftResult,
  SavePhotographerDraftInput,
  SavePhotographerResult,
  ServiceError,
} from 'api';

@Injectable()
export class MockPhotographerDraftService implements IPhotographerDraftService {
  import(
    portfolioUrl: string,
    name: string | null,
    operationKey: string,
  ): Promise<PhotographerDraftResult> {
    return this.call('import', { portfolioUrl, name, operationKey });
  }
  get(id: string): Promise<PhotographerDraftResult> {
    return this.call('get', { id });
  }
  cancel(id: string): Promise<void> {
    return this.call('cancel', { id });
  }
  save(
    id: string,
    input: SavePhotographerDraftInput,
    operationKey: string,
  ): Promise<SavePhotographerResult> {
    return this.call('save', { id, ...input, operationKey });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupePhotographerDrafts?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographerDrafts;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error);
    return result.data as T;
  }
}
