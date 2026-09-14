import { afterNextRender, Component, DOCUMENT, inject, Injector, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SESSION_SERVICE } from 'api';

@Component({
  imports: [RouterOutlet, RouterLink],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
  host: { '(document:keydown)': 'searchShortcut($event)' },
})
export class App {
  protected readonly session = inject(SESSION_SERVICE);
  private readonly router = inject(Router);
  protected readonly currentArea = signal('my-work');
  protected readonly endingSession = signal(false);
  protected readonly error = signal('');
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private lastPath = '';

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationEnd) {
        const path = event.urlAfterRedirects.split(/[?#]/)[0];
        const focusSearch =
          path === '/search' &&
          this.router.currentNavigation()?.extras.state?.['focusSearch'] === true;
        this.currentArea.set(
          ['inspiration', 'photographers', 'locations', 'search'].find(
            (area) => path === '/' + area || path.startsWith('/' + area + '/'),
          ) ?? 'my-work',
        );
        // Query-only navigation inside a search screen keeps the focus where the user left it.
        if (!['/search', '/locations/find'].includes(path) || this.lastPath !== path)
          afterNextRender(
            () => (focusSearch ? this.focusSearch() : this.document.querySelector('main')?.focus()),
            { injector: this.injector },
          );
        this.lastPath = path;
      }
    });
  }

  protected async searchShortcut(event: KeyboardEvent): Promise<void> {
    if (
      event.key !== '/' ||
      event.defaultPrevented ||
      event.repeat ||
      event.isComposing ||
      event.ctrlKey ||
      event.altKey ||
      event.metaKey ||
      event.shiftKey ||
      !this.session.current() ||
      this.document.querySelector('dialog[open], [role="dialog"][aria-modal="true"]') ||
      event
        .composedPath()
        .some(
          (target) =>
            target instanceof HTMLElement &&
            (target.isContentEditable ||
              target.matches('input, textarea, select, [role="textbox"], [role="combobox"]')),
        )
    )
      return;
    event.preventDefault();
    if (this.currentArea() === 'search') this.focusSearch();
    else await this.router.navigateByUrl('/search', { state: { focusSearch: true } });
  }

  private focusSearch(): void {
    this.document.querySelector<HTMLInputElement>('main input[type="search"]')?.focus();
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
