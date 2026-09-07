import { afterNextRender, Component, DOCUMENT, inject, Injector, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationEnd)
        afterNextRender(() => this.document.querySelector('main')?.focus(), {
          injector: this.injector,
        });
    });
  }

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
