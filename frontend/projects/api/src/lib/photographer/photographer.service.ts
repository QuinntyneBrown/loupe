import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { IPhotographerService, PhotographerReferencePage } from './photographer.service.contract';
import { PhotographerResult } from './photographer-metadata';
import { PhotographerPage } from './photographer-result';

@Injectable()
export class PhotographerService implements IPhotographerService {
  private readonly http = inject(HttpClient);
  async list(cursor?: string): Promise<PhotographerPage> {
    return this.read('/api/photographers', cursor);
  }
  get(id: string): Promise<PhotographerResult> {
    return this.read(`/api/photographers/${encodeURIComponent(id)}`);
  }
  references(id: string, cursor?: string): Promise<PhotographerReferencePage> {
    return this.read(`/api/photographers/${encodeURIComponent(id)}/references`, cursor);
  }
  private async read<T>(url: string, cursor?: string): Promise<T> {
    try {
      return await firstValueFrom(
        this.http.get<T>(url, {
          params: cursor ? { cursor } : {},
          timeout: 15000,
        }),
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
