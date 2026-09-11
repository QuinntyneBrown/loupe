import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ServiceError } from '../common/service-error';
import { SavePhotographerResult } from '../photographer/photographer-metadata';
import { PhotographerDraftResult, SavePhotographerDraftInput } from './photographer-draft-result';
import { IPhotographerDraftService } from './photographer-draft.service.contract';

@Injectable()
export class PhotographerDraftService implements IPhotographerDraftService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  import(
    portfolioUrl: string,
    name: string | null,
    operationKey: string,
  ): Promise<PhotographerDraftResult> {
    return this.request('POST', '/api/photographer-drafts', { portfolioUrl, name }, operationKey);
  }
  get(id: string): Promise<PhotographerDraftResult> {
    return this.request('GET', this.path(id));
  }
  cancel(id: string): Promise<void> {
    return this.request('DELETE', this.path(id));
  }
  save(
    id: string,
    input: SavePhotographerDraftInput,
    operationKey: string,
  ): Promise<SavePhotographerResult> {
    return this.request('POST', this.path(id) + '/save', input, operationKey);
  }
  private path(id: string): string {
    return '/api/photographer-drafts/' + encodeURIComponent(id);
  }
  private async request<T>(method: string, url: string, body?: object, key?: string): Promise<T> {
    try {
      const headers: Record<string, string> =
        method === 'GET' ? {} : { 'X-CSRF-Token': await this.session.getRequestToken() };
      if (key) headers['Idempotency-Key'] = key;
      return await firstValueFrom(
        this.http.request<T>(method, url, { body, headers, timeout: 15000 }),
      );
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
          ? error.error.code
          : 'request_failed',
      );
    }
  }
}
