import { Injectable } from '@angular/core';
import {
  IVideoService,
  ServiceError,
  VideoInput,
  VideoPage,
  VideoResult,
  VideoSearchRequest,
  VideoTagFacet,
} from 'api';

@Injectable()
export class MockVideoService implements IVideoService {
  list(request: VideoSearchRequest = {}): Promise<VideoPage> {
    return this.call('list', request);
  }
  tags(): Promise<VideoTagFacet[]> {
    return this.call('tags', {});
  }
  get(id: string): Promise<VideoResult> {
    return this.call('get', { id });
  }
  save(input: VideoInput): Promise<VideoResult> {
    return this.call('save', input);
  }
  update(id: string, input: VideoInput & { revision: number }): Promise<VideoResult> {
    return this.call('update', { id, ...input });
  }
  async delete(id: string, revision: number): Promise<void> {
    await this.call('delete', { id, revision });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeVideos?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string; errors?: Record<string, string[]> }>;
      }
    ).loupeVideos;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error, result.errors);
    return result.data as T;
  }
}
