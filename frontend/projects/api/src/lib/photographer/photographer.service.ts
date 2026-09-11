import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ServiceError } from '../common/service-error';
import { IPhotographerService } from './photographer.service.contract';
import { PhotographerPage } from './photographer-result';

@Injectable()
export class PhotographerService implements IPhotographerService {
  private readonly http = inject(HttpClient);
  async list(cursor?: string): Promise<PhotographerPage> {
    try {
      return await firstValueFrom(
        this.http.get<PhotographerPage>('/api/photographers', {
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
