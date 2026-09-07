import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ISessionService, SessionResult } from 'api';

@Injectable()
export class MockSessionService implements ISessionService {
  private readonly router = inject(Router);
  readonly current = signal<SessionResult | null>(null);
  async load(): Promise<SessionResult | null> {
    return this.current();
  }
  async signOut(): Promise<void> {
    this.current.set(null);
  }
  signIn(returnUrl: string): void {
    this.current.set({ subject: 'fixture-owner', name: 'Morgan' });
    void this.router.navigateByUrl(returnUrl);
  }
}
