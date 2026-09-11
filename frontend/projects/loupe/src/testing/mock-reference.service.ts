import { ReferenceMetadata, ReferenceLink, ReferenceLinkResult } from 'api';
import { ReferenceUpload, UploadProgress } from 'api';
import { Injectable } from '@angular/core';
import { IReferenceService, ReferencePage, ReferenceResult, ServiceError } from 'api';
import { ReferenceTag, ReferenceTagFacet } from 'api';

@Injectable()
export class MockReferenceService implements IReferenceService {
  setPhotographer(
    id: string,
    revision: number,
    photographerId: string | null,
  ): Promise<ReferenceResult> {
    return this.call('setPhotographer', { id, revision, photographerId });
  }
  updateText(
    id: string,
    revision: number,
    field: 'description' | 'notes',
    text: string,
  ): Promise<ReferenceResult> {
    return this.call('updateText', { id, revision, field, text });
  }
  async replaceImage(
    id: string,
    revision: number,
    image: File,
    operationKey: string,
  ): Promise<ReferenceResult> {
    let bytes: ArrayBuffer;
    try {
      bytes = await image.arrayBuffer();
    } catch {
      throw new ServiceError('file_unavailable');
    }
    const hash = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)), (value) =>
      value.toString(16).padStart(2, '0'),
    ).join('');
    return this.call('replaceImage', { id, revision, operationKey, hash, contentType: image.type });
  }
  setTags(
    id: string,
    revision: number,
    tags: Pick<ReferenceTag, 'name' | 'category'>[],
  ): Promise<ReferenceResult> {
    return this.call('setTags', { id, revision, tags });
  }
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
  tags(boardId?: string): Promise<ReferenceTagFacet[]> {
    return this.call('tags', { boardId });
  }
  list(cursor?: string, boardId?: string, tags: string[] = []): Promise<ReferencePage> {
    return this.call('list', { cursor, boardId, tags });
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
