import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ServiceError } from '../common/service-error';
import { ReferenceResult } from '../reference/reference-result';
import { BoardResult } from './board-result';
import { IBoardService } from './board.service.contract';

@Injectable()
export class BoardService implements IBoardService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  list(): Promise<BoardResult[]> {
    return this.request('GET', '/api/boards');
  }
  create(name: string): Promise<BoardResult> {
    return this.request('POST', '/api/boards', { name });
  }
  rename(id: string, revision: number, name: string): Promise<BoardResult> {
    return this.request('PUT', '/api/boards/' + encodeURIComponent(id), { name, revision });
  }
  delete(id: string, revision: number): Promise<void> {
    return this.request(
      'DELETE',
      '/api/boards/' + encodeURIComponent(id) + '?revision=' + revision,
    );
  }
  setMemberships(id: string, revision: number, boardIds: string[]): Promise<ReferenceResult> {
    return this.request('PUT', '/api/references/' + encodeURIComponent(id) + '/boards', {
      revision,
      boardIds,
    });
  }
  private async request<T>(method: string, url: string, body?: object): Promise<T> {
    try {
      const headers: Record<string, string> =
        method === 'GET' ? {} : { 'X-CSRF-Token': await this.session.getRequestToken() };
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
