import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ISessionService } from './session.service.contract';
import { SessionResult } from './session-result';
import { ServiceError } from '../common/service-error';

@Injectable()
export class SessionService implements ISessionService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<SessionResult | null>(null);
  readonly current = this.session.asReadonly();
  private csrf: string | null = null;
  async getRequestToken(): Promise<string> {
    if (!this.csrf) await this.load();
    if (!this.csrf) throw new ServiceError('authentication_required');
    return this.csrf;
  }

  async load(): Promise<SessionResult | null> {
    try {
      const response = await firstValueFrom(
        this.http.get<SessionResult>('/api/session', { observe: 'response' }),
      );
      this.csrf = response.headers.get('X-CSRF-Token');
      this.session.set(response.body);
      return response.body;
    } catch (error) {
      this.session.set(null);
      this.csrf = null;
      if (error instanceof HttpErrorResponse && error.status === 401) return null;
      throw error;
    }
  }

  signIn(returnUrl: string): void {
    window.location.assign(`/api/session/sign-in?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  async signOut(): Promise<void> {
    if (!this.csrf) throw new Error('The session must be refreshed before signing out.');
    await firstValueFrom(
      this.http.post('/api/session/sign-out', null, { headers: { 'X-CSRF-Token': this.csrf } }),
    );
    this.csrf = null;
    this.session.set(null);
  }
}
