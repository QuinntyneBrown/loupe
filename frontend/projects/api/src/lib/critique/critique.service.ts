import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ICritiqueService } from './critique.service.contract';
import { SavedCritique } from './critique-result';
import { ServiceError } from '../common/service-error';

@Injectable()
export class CritiqueService implements ICritiqueService {
  private readonly http = inject(HttpClient);
  async get(photographId: string): Promise<SavedCritique | null> {
    try {
      return await firstValueFrom(
        this.http.get<SavedCritique | null>(
          `/api/photographs/${encodeURIComponent(photographId)}/critique`,
          { timeout: 15000 },
        ),
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
