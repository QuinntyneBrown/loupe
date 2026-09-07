import { SESSION_SERVICE, PHOTOGRAPH_SERVICE } from 'api';
import { MockSessionService } from './mock-session.service';
import { MockPhotographService } from './mock-photograph.service';

export const appProviders = [
  { provide: SESSION_SERVICE, useClass: MockSessionService },
  { provide: PHOTOGRAPH_SERVICE, useClass: MockPhotographService },
];
