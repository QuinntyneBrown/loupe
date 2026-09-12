import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { ISearchService } from './search.service.contract';
import { SearchRequest } from './search-request';
import { SearchResult } from './search-result';
import { SearchTagFacet } from './search-tag-facet';

@Injectable()
export class SearchService implements ISearchService {
  private readonly http = inject(HttpClient);
  async tags(selectedTags: string[] = []): Promise<SearchTagFacet[]> {
    let params = new HttpParams();
    for (const tag of selectedTags) params = params.append('selectedTags', tag);
    try {
      return await firstValueFrom(
        this.http.get<SearchTagFacet[]>('/api/search/tags', { params, timeout: 15000 }),
      );
    } catch (error) {
      throw this.serviceError(error);
    }
  }
  async search(request: SearchRequest): Promise<SearchResult> {
    let params = new HttpParams().set('query', request.query).set('type', request.type);
    for (const tag of request.tags) params = params.append('tags', tag);
    for (const id of request.boardIds) params = params.append('boardIds', id);
    if (request.cursor) params = params.set('cursor', request.cursor);
    if (request.mode !== undefined) params = params.set('mode', request.mode);
    try {
      return await firstValueFrom(
        this.http.get<SearchResult>('/api/search', { params, timeout: 15000 }),
      );
    } catch (error) {
      throw this.serviceError(error);
    }
  }
  private serviceError(error: unknown): ServiceError {
    const body: unknown = error instanceof HttpErrorResponse ? error.error : null;
    if (!body || typeof body !== 'object') return new ServiceError('request_failed');
    const code = 'code' in body && typeof body.code === 'string' ? body.code : 'request_failed';
    const errors: Record<string, string[]> = {};
    if ('errors' in body && body.errors && typeof body.errors === 'object') {
      for (const [field, messages] of Object.entries(body.errors)) {
        if (
          Array.isArray(messages) &&
          messages.every((message: unknown) => typeof message === 'string')
        )
          errors[field] = messages;
      }
    }
    return new ServiceError(code, errors);
  }
}
