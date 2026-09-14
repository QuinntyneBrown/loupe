import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { ILocationSearchService } from './location-search.service.contract';
import { LocationSearchRequest } from './location-search-request';
import { LocationSearchPage, LocationTagFacet } from './location-search-result';

@Injectable()
export class LocationSearchService implements ILocationSearchService {
  private readonly http = inject(HttpClient);
  async tags(): Promise<LocationTagFacet[]> {
    try {
      return await firstValueFrom(
        this.http.get<LocationTagFacet[]>('/api/locations/search/tags', { timeout: 15000 }),
      );
    } catch (error) {
      throw this.serviceError(error);
    }
  }
  async search(request: LocationSearchRequest): Promise<LocationSearchPage> {
    let params = new HttpParams().set('query', request.query).set('mode', request.mode);
    for (const shootType of request.shootTypes) params = params.append('shootTypes', shootType);
    if (request.people !== null) params = params.set('people', request.people);
    for (const period of request.timesOfDay) params = params.append('timesOfDay', period);
    if (request.setting) params = params.set('setting', request.setting);
    for (const tag of request.tags) params = params.append('tags', tag);
    if (request.cursor) params = params.set('cursor', request.cursor);
    try {
      return await firstValueFrom(
        this.http.get<LocationSearchPage>('/api/locations/search', { params, timeout: 15000 }),
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
