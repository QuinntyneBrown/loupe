import { Injectable } from '@angular/core';
import {
  IReferenceDraftService,
  ReferenceDraftResult,
  ReferenceMetadata,
  ReferenceLinkResult,
  ServiceError,
  UploadProgress,
} from 'api';

@Injectable()
export class MockReferenceDraftService implements IReferenceDraftService {
  import(sourceUrl: string, operationKey: string): Promise<ReferenceDraftResult> {
    return this.call('import', { sourceUrl, operationKey });
  }
  async upload(
    image: File,
    sourceUrl: string,
    operationKey: string,
    progress?: (value: UploadProgress) => void,
  ): Promise<ReferenceDraftResult> {
    const report = (event: Event) => {
      if (event instanceof CustomEvent) progress?.(event.detail as UploadProgress);
    };
    window.addEventListener('loupe-reference-draft-upload-progress', report);
    try {
      return await this.call('upload', { filename: image.name, sourceUrl, operationKey });
    } finally {
      window.removeEventListener('loupe-reference-draft-upload-progress', report);
    }
  }
  get(id: string): Promise<ReferenceDraftResult> {
    return this.call('get', { id });
  }
  cancel(id: string): Promise<void> {
    return this.call('cancel', { id });
  }
  save(
    id: string,
    revision: number,
    metadata: ReferenceMetadata,
    boardIds: string[],
    operationKey: string,
  ): Promise<ReferenceLinkResult> {
    return this.call('save', { id, revision, ...metadata, boardIds, operationKey });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeReferenceDrafts?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupeReferenceDrafts;
    if (!callback) throw new ServiceError('item_unavailable');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error);
    return result.data as T;
  }
}
