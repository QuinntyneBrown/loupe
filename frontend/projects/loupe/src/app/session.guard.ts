import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SESSION_SERVICE } from 'api';

export const sessionGuard: CanActivateFn = async (_route, state) => {
  const session = inject(SESSION_SERVICE);
  const router = inject(Router);
  try {
    return (await session.load())
      ? true
      : router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
  } catch {
    return router.createUrlTree(['/sign-in'], {
      queryParams: { returnUrl: state.url, error: 'session_unavailable' },
    });
  }
};
