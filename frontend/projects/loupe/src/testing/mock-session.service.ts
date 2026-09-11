import { Injectable, signal } from '@angular/core';
import { ISessionService, SessionResult, SignInCredentials, ServiceError } from 'api';

@Injectable()
export class MockSessionService implements ISessionService {
  async getRequestToken(): Promise<string> {
    return 'fixture-only-request-token';
  }
  readonly current = signal<SessionResult | null>(null);
  async load(): Promise<SessionResult | null> {
    const fixture = (window as Window & { loupeFixture?: { sessionUnavailable?: boolean } })
      .loupeFixture;
    if (fixture?.sessionUnavailable) {
      fixture.sessionUnavailable = false;
      throw new Error('Controlled session service failure');
    }
    return this.current();
  }
  async signOut(): Promise<void> {
    this.current.set(null);
  }
  async signIn(credentials: SignInCredentials): Promise<SessionResult> {
    const fixture = (
      window as Window & { loupeFixture?: { signInUnavailable?: boolean; signInDelayMs?: number } }
    ).loupeFixture;
    if (fixture?.signInDelayMs)
      await new Promise((resolve) => setTimeout(resolve, fixture.signInDelayMs));
    if (fixture?.signInUnavailable) {
      fixture.signInUnavailable = false;
      throw new ServiceError('sign_in_unavailable');
    }
    if (
      credentials.email !== 'photographer@example.com' ||
      credentials.password !== 'local acceptance password'
    ) {
      throw new ServiceError('invalid_credentials');
    }
    const session = { subject: 'fixture-owner', name: 'Morgan' };
    this.current.set(session);
    return session;
  }
}
