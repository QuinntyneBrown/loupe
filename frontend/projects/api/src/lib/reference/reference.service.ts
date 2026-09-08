import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IReferenceService } from './reference.service.contract';
import { ReferencePage, ReferenceResult } from './reference-result';
import { ServiceError } from '../common/service-error';

@Injectable()
export class ReferenceService implements IReferenceService {
  private readonly http = inject(HttpClient);
  list(cursor?: string): Promise<ReferencePage> {
    return this.read('/api/references', cursor ? { cursor } : {});
  }
  get(id: string): Promise<ReferenceResult> {
    return this.read('/api/references/' + encodeURIComponent(id));
  }
  private async read<T>(url: string, params: Record<string, string> = {}): Promise<T> {
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
