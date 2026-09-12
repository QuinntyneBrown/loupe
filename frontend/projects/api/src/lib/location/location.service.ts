import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { ILocationService } from './location.service.contract';
import { LocationPage } from './location-result';

@Injectable()
export class LocationService implements ILocationService {
  private readonly http = inject(HttpClient);
  list(cursor?: string): Promise<LocationPage> {
    return this.read('/api/locations', cursor ? { cursor } : {});
  }
  private async read<T>(url: string, params: Record<string, string>): Promise<T> {
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
