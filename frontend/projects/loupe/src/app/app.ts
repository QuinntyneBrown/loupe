import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { SESSION_SERVICE } from 'api';

@Component({
  imports: [RouterOutlet, RouterLink],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly session = inject(SESSION_SERVICE);
  private readonly router = inject(Router);
  protected readonly endingSession = signal(false);
  protected readonly error = signal('');

  protected async signOut(): Promise<void> {
    this.endingSession.set(true);
    this.error.set('');
    try {
      await this.session.signOut();
      await this.router.navigateByUrl('/sign-in', { replaceUrl: true });
    } catch {
      this.error.set('Sign out could not be completed. Please try again.');
    } finally {
      this.endingSession.set(false);
    }
  }
}
