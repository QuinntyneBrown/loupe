import { Injectable } from '@angular/core';
import {
  ILocationService,
  LocationDetailsInput,
  LocationInput,
  LocationPage,
  LocationResult,
  LocationTagInput,
  LocationTextField,
  ServiceError,
  UploadProgress,
} from 'api';

@Injectable()
export class MockLocationService implements ILocationService {
  list(cursor?: string): Promise<LocationPage> {
    return this.call('list', { cursor });
  }
  get(id: string): Promise<LocationResult> {
    return this.call('get', { id });
  }
  create(input: LocationInput, operationKey: string): Promise<LocationResult> {
    return this.call('create', { ...input, operationKey });
  }
  update(id: string, input: LocationDetailsInput & { revision: number }): Promise<LocationResult> {
    return this.call('update', { id, ...input });
  }
  updateText(
    id: string,
    revision: number,
    field: LocationTextField,
    text: string | null,
  ): Promise<LocationResult> {
    return this.call('updateText', { id, revision, field, text });
  }
  setTags(id: string, revision: number, tags: LocationTagInput[]): Promise<LocationResult> {
    return this.call('setTags', { id, revision, tags });
  }
  async addImage(
    id: string,
    image: File,
    operationKey: string,
    onProgress?: (progress: UploadProgress) => void,
    signal?: AbortSignal,
  ): Promise<LocationResult> {
    if (signal?.aborted) throw new ServiceError('upload_canceled');
    const report = (event: Event) => {
      if (signal?.aborted || !(event instanceof CustomEvent)) return;
      const detail = event.detail as UploadProgress & { operationKey?: string };
      if (!detail.operationKey || detail.operationKey === operationKey)
        onProgress?.({ transferred: detail.transferred, total: detail.total });
    };
    let abort: (() => void) | undefined;
    const canceled = new Promise<never>((_, reject) => {
      abort = () => {
        void this.call('abortUpload', { operationKey });
        reject(new ServiceError('upload_canceled'));
      };
      signal?.addEventListener('abort', abort, { once: true });
    });
    window.addEventListener('loupe-location-upload-progress', report);
    try {
      return await Promise.race([
        canceled,
        this.call<LocationResult>('addImage', {
          id,
          filename: image.name,
          contentType: image.type,
          size: image.size,
          operationKey,
        }),
      ]);
    } finally {
      if (abort) signal?.removeEventListener('abort', abort);
      window.removeEventListener('loupe-location-upload-progress', report);
    }
  }
  removeImage(id: string, imageId: string, revision: number): Promise<LocationResult> {
    return this.call('removeImage', { id, imageId, revision });
  }
  setCover(id: string, imageId: string, revision: number): Promise<LocationResult> {
    return this.call('setCover', { id, imageId, revision });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeLocations?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string; errors?: Record<string, string[]> }>;
      }
    ).loupeLocations;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error, result.errors);
    return result.data as T;
  }
}
