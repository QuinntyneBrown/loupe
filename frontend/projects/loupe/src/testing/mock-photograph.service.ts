import { Injectable } from '@angular/core';
import {
  IPhotographService,
  PhotographPage,
  PhotographResult,
  ServiceError,
  CritiqueBrief,
  PhotographUpload,
  UploadProgress,
} from 'api';

@Injectable()
export class MockPhotographService implements IPhotographService {
  async upload(
    input: PhotographUpload,
    onProgress?: (progress: UploadProgress) => void,
  ): Promise<PhotographResult> {
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
    window.addEventListener('loupe-upload-progress', report);
    try {
      return await this.request<PhotographResult>('upload', {
        filename: input.image.name,
        title: input.title,
        brief: input.brief,
        operationKey: input.operationKey,
        hash,
        contentType: input.image.type,
      });
    } finally {
      window.removeEventListener('loupe-upload-progress', report);
    }
  }
  updateBrief(id: string, revision: number, brief: CritiqueBrief): Promise<PhotographResult> {
    return this.request<PhotographResult>('updateBrief', { id, revision, ...brief });
  }
  updateNotes(id: string, revision: number, notes: string): Promise<PhotographResult> {
    return this.request<PhotographResult>('updateNotes', { id, revision, notes });
  }
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
