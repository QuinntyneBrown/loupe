import { ReferenceLink, ReferenceLinkResult } from './reference-link';
import { ReferenceMetadata } from './reference-metadata';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { UploadProgress } from '../common/upload-progress';
import { ReferenceUpload } from './reference-upload';
import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpEventType, HttpResponse } from '@angular/common/http';
import { filter, firstValueFrom, map, tap } from 'rxjs';
import { IReferenceService } from './reference.service.contract';
import { ReferencePage, ReferenceResult } from './reference-result';
import { ServiceError } from '../common/service-error';

@Injectable()
export class ReferenceService implements IReferenceService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  async saveLink(input: ReferenceLink): Promise<ReferenceLinkResult> {
    const token = await this.session.getRequestToken();
    const { operationKey, ...metadata } = input;
    try {
      return await firstValueFrom(
        this.http.post<ReferenceLinkResult>('/api/references/links', metadata, {
          headers: { 'X-CSRF-Token': token, 'Idempotency-Key': operationKey },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
  async upload(
    input: ReferenceUpload,
    onProgress?: (progress: UploadProgress) => void,
  ): Promise<ReferenceResult> {
    try {
      await input.image.slice(0, 1).arrayBuffer();
    } catch {
      throw new ServiceError('file_unavailable');
    }
    const token = await this.session.getRequestToken();
    const body = new FormData();
    body.append('image', input.image, input.image.name);
    for (const field of ['title', 'sourceUrl', 'attribution', 'notes'] as const)
      body.append(field, input[field]);
    try {
      return await firstValueFrom(
        this.http
          .post<ReferenceResult>('/api/references/images', body, {
            headers: { 'X-CSRF-Token': token, 'Idempotency-Key': input.operationKey },
            reportProgress: true,
            observe: 'events',
          })
          .pipe(
            tap((event) => {
              if (event.type === HttpEventType.UploadProgress)
                onProgress?.({ transferred: event.loaded, total: event.total ?? null });
            }),
            filter(
              (event): event is HttpResponse<ReferenceResult> => event instanceof HttpResponse,
            ),
            map((response) => {
              if (!response.body) throw new ServiceError('request_failed');
              return response.body;
            }),
          ),
      );
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
  async update(
    id: string,
    revision: number,
    metadata: ReferenceMetadata,
  ): Promise<ReferenceResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.put<ReferenceResult>(
          '/api/references/' + encodeURIComponent(id),
          { revision, ...metadata },
          { headers: { 'X-CSRF-Token': token }, timeout: 15000 },
        ),
      );
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
  list(cursor?: string): Promise<ReferencePage> {
    return this.read('/api/references', cursor ? { cursor } : {});
  }
  get(id: string): Promise<ReferenceResult> {
    return this.read('/api/references/' + encodeURIComponent(id));
  }
  private async read<T>(url: string, params: Record<string, string> = {}): Promise<T> {
    try {
      return await firstValueFrom(this.http.get<T>(url, { params, timeout: 15000 }));
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
}
