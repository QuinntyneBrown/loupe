import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpEventType, HttpResponse } from '@angular/common/http';
import { filter, firstValueFrom, fromEvent, map, NEVER, takeUntil, tap } from 'rxjs';
import { IPhotographService } from './photograph.service.contract';
import { PhotographPage } from './photograph-page';
import { PhotographResult } from './photograph-result';
import { ServiceError } from '../common/service-error';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { CritiqueBrief } from './critique-brief';
import { PhotographUpload } from './photograph-upload';
import { UploadProgress } from '../common/upload-progress';

@Injectable()
export class PhotographService implements IPhotographService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  async upload(
    input: PhotographUpload,
    onProgress?: (progress: UploadProgress) => void,
    signal?: AbortSignal,
  ): Promise<PhotographResult> {
    if (signal?.aborted) throw new ServiceError('upload_canceled');
    try {
      await input.image.slice(0, 1).arrayBuffer();
    } catch {
      throw new ServiceError('file_unavailable');
    }
    const token = await this.session.getRequestToken();
    if (signal?.aborted) throw new ServiceError('upload_canceled');
    const body = new FormData();
    body.append('image', input.image, input.image.name);
    body.append('title', input.title);
    for (const [field, value] of Object.entries(input.brief)) {
      if (value !== null) body.append(field, value);
    }
    try {
      return await firstValueFrom(
        this.http
          .post<PhotographResult>('/api/photographs', body, {
            headers: { 'X-CSRF-Token': token, 'Idempotency-Key': input.operationKey },
            reportProgress: true,
            observe: 'events',
          })
          .pipe(
            takeUntil(signal ? fromEvent(signal, 'abort') : NEVER),
            tap((event) => {
              if (event.type === HttpEventType.UploadProgress)
                onProgress?.({ transferred: event.loaded, total: event.total ?? null });
            }),
            filter(
              (event): event is HttpResponse<PhotographResult> => event instanceof HttpResponse,
            ),
            map((response) => {
              if (!response.body) throw new ServiceError('request_failed');
              return response.body;
            }),
          ),
      );
    } catch (error) {
      if (signal?.aborted) throw new ServiceError('upload_canceled');
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
  updateNotes(id: string, revision: number, notes: string): Promise<PhotographResult> {
    return this.update(id, 'notes', { revision, notes });
  }
  updateBrief(id: string, revision: number, brief: CritiqueBrief): Promise<PhotographResult> {
    return this.update(id, 'brief', { revision, ...brief });
  }
  private async update(
    id: string,
    field: 'notes' | 'brief',
    body: object,
  ): Promise<PhotographResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.put<PhotographResult>(
          `/api/photographs/${encodeURIComponent(id)}/${field}`,
          body,
          { headers: { 'X-CSRF-Token': token } },
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
  async get(id: string): Promise<PhotographResult> {
    try {
      return await firstValueFrom(
        this.http.get<PhotographResult>(`/api/photographs/${encodeURIComponent(id)}`),
      );
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && error.status === 404
          ? 'item_unavailable'
          : 'request_failed',
      );
    }
  }
  list(cursor?: string): Promise<PhotographPage> {
    return firstValueFrom(
      this.http.get<PhotographPage>('/api/photographs', {
        params: cursor ? { cursor } : {},
      }),
    );
  }
}
