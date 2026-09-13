import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { IVideoService } from './video.service.contract';
import { VideoInput } from './video-input';
import { VideoPage, VideoResult, VideoTagFacet } from './video-result';
import { VideoSearchRequest } from './video-search-request';

@Injectable()
export class VideoService implements IVideoService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  list(request: VideoSearchRequest = {}): Promise<VideoPage> {
    return this.call(() =>
      this.http.get<VideoPage>('/api/videos', {
        params: {
          ...(request.query ? { query: request.query } : {}),
          ...(request.topic ? { topic: request.topic } : {}),
          ...(request.tags?.length ? { tags: request.tags } : {}),
          ...(request.mode ? { mode: request.mode } : {}),
          ...(request.cursor ? { cursor: request.cursor } : {}),
        },
        timeout: 15000,
      }),
    );
  }
  tags(): Promise<VideoTagFacet[]> {
    return this.call(() => this.http.get<VideoTagFacet[]>('/api/videos/tags', { timeout: 15000 }));
  }
  get(id: string): Promise<VideoResult> {
    return this.call(() =>
      this.http.get<VideoResult>(`/api/videos/${encodeURIComponent(id)}`, { timeout: 15000 }),
    );
  }
  async save(input: VideoInput): Promise<VideoResult> {
    const headers = { 'X-CSRF-Token': await this.session.getRequestToken() };
    return this.call(() =>
      this.http.post<VideoResult>('/api/videos', input, { headers, timeout: 15000 }),
    );
  }
  async update(id: string, input: VideoInput & { revision: number }): Promise<VideoResult> {
    const headers = { 'X-CSRF-Token': await this.session.getRequestToken() };
    return this.call(() =>
      this.http.put<VideoResult>(`/api/videos/${encodeURIComponent(id)}`, input, {
        headers,
        timeout: 15000,
      }),
    );
  }
  async delete(id: string, revision: number): Promise<void> {
    const headers = { 'X-CSRF-Token': await this.session.getRequestToken() };
    await this.call(() =>
      this.http.delete<void>(`/api/videos/${encodeURIComponent(id)}`, {
        headers,
        params: { revision: String(revision) },
        timeout: 15000,
      }),
    );
  }
  private async call<T>(request: () => import('rxjs').Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request());
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
        error instanceof HttpErrorResponse && error.error?.errors ? error.error.errors : {},
      );
    }
  }
}
