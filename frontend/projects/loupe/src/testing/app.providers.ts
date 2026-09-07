import { SESSION_SERVICE, PHOTOGRAPH_SERVICE, DELETION_SERVICE } from 'api';
import { MockDeletionService } from './mock-deletion.service';
import { MockSessionService } from './mock-session.service';
import { MockPhotographService } from './mock-photograph.service';

export const appProviders = [
  { provide: SESSION_SERVICE, useClass: MockSessionService },
  { provide: PHOTOGRAPH_SERVICE, useClass: MockPhotographService },
  { provide: DELETION_SERVICE, useClass: MockDeletionService },
];
