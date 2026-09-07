import { provideHttpClient } from '@angular/common/http';
import { SESSION_SERVICE, SessionService } from 'api';

export const appProviders = [
  provideHttpClient(),
  { provide: SESSION_SERVICE, useClass: SessionService },
];
