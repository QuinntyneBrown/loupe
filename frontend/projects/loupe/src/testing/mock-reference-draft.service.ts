import { Injectable } from '@angular/core';
import {
  IReferenceDraftService,
  ReferenceDraftResult,
  ReferenceMetadata,
  ReferenceLinkResult,
  ServiceError,
} from 'api';

@Injectable()
export class MockReferenceDraftService implements IReferenceDraftService {
  upload(image: File, sourceUrl: string, operationKey: string): Promise<ReferenceDraftResult> {
    return this.call('upload', { filename: image.name, sourceUrl, operationKey });
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
