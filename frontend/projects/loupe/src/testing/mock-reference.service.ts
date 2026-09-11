import { ReferenceMetadata, ReferenceLink, ReferenceLinkResult } from 'api';
import { ReferenceUpload, UploadProgress } from 'api';
import { Injectable } from '@angular/core';
import { IReferenceService, ReferencePage, ReferenceResult, ServiceError } from 'api';

@Injectable()
export class MockReferenceService implements IReferenceService {
  saveLink(input: ReferenceLink): Promise<ReferenceLinkResult> {
    return this.call('saveLink', input);
  }
  async upload(
    input: ReferenceUpload,
    onProgress?: (progress: UploadProgress) => void,
  ): Promise<ReferenceResult> {
    let bytes: ArrayBuffer;
    try {
      bytes = await input.image.arrayBuffer();
    } catch {
      throw new ServiceError('file_unavailable');
    }
    const digest = await crypto.subtle.digest('SHA-256', bytes);
    const hash = Array.from(new Uint8Array(digest), (value) =>
      value.toString(16).padStart(2, '0'),
    ).join('');
    const report = (event: Event) => {
      if (event instanceof CustomEvent) onProgress?.(event.detail as UploadProgress);
    };
    window.addEventListener('loupe-reference-upload-progress', report);
    try {
      return await this.call<ReferenceResult>('upload', {
        filename: input.image.name,
        title: input.title,
        sourceUrl: input.sourceUrl,
        attribution: input.attribution,
        notes: input.notes,
        operationKey: input.operationKey,
        hash,
        contentType: input.image.type,
      });
    } finally {
      window.removeEventListener('loupe-reference-upload-progress', report);
    }
  }
  update(id: string, revision: number, metadata: ReferenceMetadata): Promise<ReferenceResult> {
    return this.call('update', { id, revision, ...metadata });
  }
  list(cursor?: string, boardId?: string): Promise<ReferencePage> {
    return this.call('list', { cursor, boardId });
  }
  get(id: string): Promise<ReferenceResult> {
    return this.call('get', { id });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeReferences?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupeReferences;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data as T;
  }
}
