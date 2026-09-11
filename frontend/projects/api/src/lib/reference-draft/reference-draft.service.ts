import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpEventType, HttpResponse } from '@angular/common/http';
import { filter, firstValueFrom, map, tap } from 'rxjs';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ServiceError } from '../common/service-error';
import { UploadProgress } from '../common/upload-progress';
import { ReferenceMetadata } from '../reference/reference-metadata';
import { ReferenceLinkResult } from '../reference/reference-link';
import { ReferenceDraftResult } from './reference-draft-result';
import { IReferenceDraftService } from './reference-draft.service.contract';

@Injectable()
export class ReferenceDraftService implements IReferenceDraftService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  async upload(
    image: File,
    sourceUrl: string,
    operationKey: string,
    progress?: (value: UploadProgress) => void,
  ): Promise<ReferenceDraftResult> {
    try {
      await image.slice(0, 1).arrayBuffer();
    } catch {
      throw new ServiceError('file_unavailable');
    }
    const token = await this.session.getRequestToken();
    const body = new FormData();
    body.append('image', image, image.name);
    body.append('sourceUrl', sourceUrl);
    try {
      return await firstValueFrom(
        this.http
          .post<ReferenceDraftResult>('/api/reference-drafts/images', body, {
            headers: { 'X-CSRF-Token': token, 'Idempotency-Key': operationKey },
            observe: 'events',
            reportProgress: true,
          })
          .pipe(
            tap((event) => {
              if (event.type === HttpEventType.UploadProgress)
                progress?.({ transferred: event.loaded, total: event.total ?? null });
            }),
            filter(
              (event): event is HttpResponse<ReferenceDraftResult> => event instanceof HttpResponse,
            ),
            map((response) => {
              if (!response.body) throw new ServiceError('request_failed');
              return response.body;
            }),
          ),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async get(id: string): Promise<ReferenceDraftResult> {
    try {
      return await firstValueFrom(
        this.http.get<ReferenceDraftResult>(this.path(id), { timeout: 15000 }),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async cancel(id: string): Promise<void> {
    const token = await this.session.getRequestToken();
    try {
      await firstValueFrom(
        this.http.delete<void>(this.path(id), {
          headers: { 'X-CSRF-Token': token },
          timeout: 15000,
        }),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) return;
      throw this.failure(error);
    }
  }
  async save(
    id: string,
    revision: number,
    metadata: ReferenceMetadata,
    boardIds: string[],
    operationKey: string,
  ): Promise<ReferenceLinkResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.post<ReferenceLinkResult>(
          this.path(id) + '/save',
          { revision, ...metadata, boardIds },
          {
            headers: { 'X-CSRF-Token': token, 'Idempotency-Key': operationKey },
            timeout: 15000,
          },
        ),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  private path(id: string): string {
    return '/api/reference-drafts/' + encodeURIComponent(id);
  }
  private failure(error: unknown): ServiceError {
    return new ServiceError(
      error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
        ? error.error.code
        : 'request_failed',
    );
  }
}
