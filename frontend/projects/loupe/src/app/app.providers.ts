import { provideHttpClient, withXhr } from '@angular/common/http';
import { SESSION_SERVICE, SessionService, PHOTOGRAPH_SERVICE, PhotographService } from 'api';
import { DELETION_SERVICE, DeletionService } from 'api';

export const appProviders = [
  provideHttpClient(withXhr()),
  { provide: SESSION_SERVICE, useClass: SessionService },
  { provide: PHOTOGRAPH_SERVICE, useClass: PhotographService },
  { provide: DELETION_SERVICE, useClass: DeletionService },
];
