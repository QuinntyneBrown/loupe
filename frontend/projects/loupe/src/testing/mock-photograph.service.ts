import { Injectable } from '@angular/core';
import { IPhotographService, PhotographPage, PhotographResult, ServiceError } from 'api';

@Injectable()
export class MockPhotographService implements IPhotographService {
  async list(cursor?: string): Promise<PhotographPage> {
    if (!this.callback) return { items: [], nextCursor: null };
    return this.request<PhotographPage>('list', { cursor });
  }
  get(id: string): Promise<PhotographResult> {
    return this.request<PhotographResult>('get', { id });
  }
  private get callback() {
    return (
      window as Window & {
        loupePhotographs?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographs;
  }
  private async request<T>(operation: string, input: object): Promise<T> {
    if (!this.callback) throw new ServiceError('item_unavailable');
    const response = await this.callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data as T;
  }
}
