import { InjectionToken, Signal } from '@angular/core';
import { SignInCredentials } from './sign-in-credentials';
import { SessionResult } from './session-result';

export interface ISessionService {
  readonly current: Signal<SessionResult | null>;
  load(): Promise<SessionResult | null>;
  signIn(credentials: SignInCredentials): Promise<SessionResult>;
  signOut(): Promise<void>;
  getRequestToken(): Promise<string>;
}

export const SESSION_SERVICE = new InjectionToken<ISessionService>('SESSION_SERVICE');
