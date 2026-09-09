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
    signal?: AbortSignal,
  ): Promise<PhotographResult> {
    if (signal?.aborted) throw new ServiceError('upload_canceled');
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
      if (!signal?.aborted && event instanceof CustomEvent)
        onProgress?.(event.detail as UploadProgress);
    };
    if (signal?.aborted) throw new ServiceError('upload_canceled');
    let abort: (() => void) | undefined;
    const canceled = new Promise<never>((_, reject) => {
      abort = () => {
        void this.callback?.('abortUpload', { operationKey: input.operationKey });
        reject(new ServiceError('upload_canceled'));
      };
      signal?.addEventListener('abort', abort, { once: true });
    });
    window.addEventListener('loupe-upload-progress', report);
    try {
      return await Promise.race([
        canceled,
        this.request<PhotographResult>('upload', {
          filename: input.image.name,
          title: input.title,
          brief: input.brief,
          operationKey: input.operationKey,
          hash,
          contentType: input.image.type,
        }),
      ]);
    } finally {
      if (abort) signal?.removeEventListener('abort', abort);
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
    try {
      const response = await this.callback(operation, input);
      if (response.error) throw new ServiceError(response.error);
      return response.data as T;
    } finally {
      if (operation === 'list') window.dispatchEvent(new Event('loupe-list-settled'));
    }
  }
}
