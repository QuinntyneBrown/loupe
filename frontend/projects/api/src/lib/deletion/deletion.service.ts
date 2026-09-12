import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IDeletionService } from './deletion.service.contract';
import { DeletionResult } from './deletion-result';
import { SESSION_SERVICE } from '../session/session.service.contract';
import { ServiceError } from '../common/service-error';

@Injectable()
export class DeletionService implements IDeletionService {
  async deletePhotographer(id: string, revision: number): Promise<DeletionResult> {
    try {
      return await firstValueFrom(
        this.http.delete<DeletionResult>(`/api/photographers/${encodeURIComponent(id)}`, {
          params: { revision },
          headers: { 'X-CSRF-Token': await this.session.getRequestToken() },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async deleteReference(id: string, revision: number): Promise<DeletionResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.delete<DeletionResult>(`/api/references/${encodeURIComponent(id)}`, {
          params: { revision },
          headers: { 'X-CSRF-Token': token },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async deleteLocation(id: string, revision: number): Promise<DeletionResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.delete<DeletionResult>(`/api/locations/${encodeURIComponent(id)}`, {
          params: { revision },
          headers: { 'X-CSRF-Token': token },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  private readonly http = inject(HttpClient);
  private readonly session = inject(SESSION_SERVICE);
  async deletePhotograph(id: string, revision: number): Promise<DeletionResult> {
    const token = await this.session.getRequestToken();
    try {
      return await firstValueFrom(
        this.http.delete<DeletionResult>(`/api/photographs/${encodeURIComponent(id)}`, {
          params: { revision },
          headers: { 'X-CSRF-Token': token },
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  async get(id: string): Promise<DeletionResult> {
    try {
      return await firstValueFrom(
        this.http.get<DeletionResult>(`/api/deletions/${encodeURIComponent(id)}`, {
          timeout: 15000,
        }),
      );
    } catch (error) {
      throw this.failure(error);
    }
  }
  private failure(error: unknown): ServiceError {
    return new ServiceError(
      error instanceof HttpErrorResponse && typeof error.error?.code === 'string'
        ? error.error.code
        : 'request_failed',
    );
  }
}
