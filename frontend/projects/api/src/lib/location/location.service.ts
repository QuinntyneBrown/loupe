import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ILocationService } from './location.service.contract';
import { LocationPage, LocationResult } from './location-result';
import { LocationInput } from './location-input';

@Injectable()
export class LocationService implements ILocationService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  list(cursor?: string): Promise<LocationPage> {
    return this.read('/api/locations', cursor ? { cursor } : {});
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
