import { provideHttpClient } from '@angular/common/http';
import { SESSION_SERVICE, SessionService, PHOTOGRAPH_SERVICE, PhotographService } from 'api';

export const appProviders = [
  provideHttpClient(),
  { provide: SESSION_SERVICE, useClass: SessionService },
  { provide: PHOTOGRAPH_SERVICE, useClass: PhotographService },
];
