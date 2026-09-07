import { SESSION_SERVICE } from 'api';
import { MockSessionService } from './mock-session.service';

export const appProviders = [{ provide: SESSION_SERVICE, useClass: MockSessionService }];
