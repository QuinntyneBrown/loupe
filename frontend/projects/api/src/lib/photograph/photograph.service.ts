import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IPhotographService } from './photograph.service.contract';
import { PhotographPage } from './photograph-page';
import { PhotographResult } from './photograph-result';
import { ServiceError } from '../common/service-error';

@Injectable()
export class PhotographService implements IPhotographService {
  private readonly http = inject(HttpClient);
  async get(id: string): Promise<PhotographResult> {
    try {
      return await firstValueFrom(
        this.http.get<PhotographResult>(`/api/photographs/${encodeURIComponent(id)}`),
      );
    } catch (error) {
      throw new ServiceError(
        error instanceof HttpErrorResponse && error.status === 404
          ? 'item_unavailable'
          : 'request_failed',
      );
    }
  }
  list(cursor?: string): Promise<PhotographPage> {
    return firstValueFrom(
      this.http.get<PhotographPage>('/api/photographs', {
        params: cursor ? { cursor } : {},
      }),
    );
  }
}
