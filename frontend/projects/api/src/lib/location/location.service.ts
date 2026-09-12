import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpEventType, HttpResponse } from '@angular/common/http';
import { filter, firstValueFrom, fromEvent, map, NEVER, takeUntil, tap } from 'rxjs';
import { UploadProgress } from '../common/upload-progress';
import { ServiceError } from '../common/service-error';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ILocationService, LocationTextField } from './location.service.contract';
import { LocationPage, LocationResult } from './location-result';
import { LocationDetailsInput, LocationInput, LocationTagInput } from './location-input';

@Injectable()
export class LocationService implements ILocationService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  list(cursor?: string): Promise<LocationPage> {
    return this.read('/api/locations', cursor ? { cursor } : {});
  }
  get(id: string): Promise<LocationResult> {
    return this.read(`/api/locations/${encodeURIComponent(id)}`, {});
  }
  async create(input: LocationInput, operationKey: string): Promise<LocationResult> {
    try {
      return await firstValueFrom(
        this.http.post<LocationResult>('/api/locations', input, {
          headers: {
            'Idempotency-Key': operationKey,
            'X-CSRF-Token': await this.session.getRequestToken(),
          },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw LocationService.failure(error);
    }
  }
  update(id: string, input: LocationDetailsInput & { revision: number }): Promise<LocationResult> {
    return this.put(`/api/locations/${encodeURIComponent(id)}`, input);
  }
  updateText(
    id: string,
    revision: number,
    field: LocationTextField,
    text: string | null,
  ): Promise<LocationResult> {
    const path = field === 'scoutingBrief' ? 'scouting-brief' : 'notes';
    return this.put(`/api/locations/${encodeURIComponent(id)}/${path}`, { revision, text });
  }
  setTags(id: string, revision: number, tags: LocationTagInput[]): Promise<LocationResult> {
    return this.put(`/api/locations/${encodeURIComponent(id)}/tags`, { revision, tags });
  }
  async addImage(
    id: string,
    image: File,
    operationKey: string,
    onProgress?: (progress: UploadProgress) => void,
    signal?: AbortSignal,
  ): Promise<LocationResult> {
    if (signal?.aborted) throw new ServiceError('upload_canceled');
    try {
      await image.slice(0, 1).arrayBuffer();
    } catch {
      throw new ServiceError('file_unavailable');
    }
    const token = await this.session.getRequestToken();
    if (signal?.aborted) throw new ServiceError('upload_canceled');
    const body = new FormData();
    body.append('image', image, image.name);
    try {
      return await firstValueFrom(
        this.http
          .post<LocationResult>(`/api/locations/${encodeURIComponent(id)}/images`, body, {
            headers: { 'X-CSRF-Token': token, 'Idempotency-Key': operationKey },
            reportProgress: true,
            observe: 'events',
          })
          .pipe(
            takeUntil(signal ? fromEvent(signal, 'abort') : NEVER),
            tap((event) => {
              if (event.type === HttpEventType.UploadProgress)
                onProgress?.({ transferred: event.loaded, total: event.total ?? null });
            }),
            filter((event): event is HttpResponse<LocationResult> => event instanceof HttpResponse),
            map((response) => {
              if (!response.body) throw new ServiceError('request_failed');
              return response.body;
            }),
          ),
      );
    } catch (error) {
      if (signal?.aborted) throw new ServiceError('upload_canceled');
      throw LocationService.failure(error);
    }
  }
  async removeImage(id: string, imageId: string, revision: number): Promise<LocationResult> {
    try {
      return await firstValueFrom(
        this.http.delete<LocationResult>(
          `/api/locations/${encodeURIComponent(id)}/images/${encodeURIComponent(imageId)}`,
          {
            params: { revision },
            headers: { 'X-CSRF-Token': await this.session.getRequestToken() },
            timeout: 15000,
          },
        ),
      );
    } catch (error) {
      throw LocationService.failure(error);
    }
  }
  setCover(id: string, imageId: string, revision: number): Promise<LocationResult> {
    return this.put(`/api/locations/${encodeURIComponent(id)}/cover`, { revision, imageId });
  }
  private async put(url: string, body: object): Promise<LocationResult> {
    try {
      return await firstValueFrom(
        this.http.put<LocationResult>(url, body, {
          headers: { 'X-CSRF-Token': await this.session.getRequestToken() },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw LocationService.failure(error);
    }
  }
  private async read<T>(url: string, params: Record<string, string>): Promise<T> {
    try {
      return await firstValueFrom(this.http.get<T>(url, { params, timeout: 15000 }));
    } catch (error) {
      throw LocationService.failure(error);
    }
  }
  private static failure(error: unknown): ServiceError {
    const body: unknown = error instanceof HttpErrorResponse ? error.error : null;
    if (!body || typeof body !== 'object') return new ServiceError('request_failed');
    const code = 'code' in body && typeof body.code === 'string' ? body.code : 'request_failed';
    const errors: Record<string, string[]> = {};
    if ('errors' in body && body.errors && typeof body.errors === 'object')
      for (const [field, messages] of Object.entries(body.errors))
        if (Array.isArray(messages) && messages.every((message) => typeof message === 'string'))
          errors[field] = messages;
    return new ServiceError(code, errors);
  }
}
