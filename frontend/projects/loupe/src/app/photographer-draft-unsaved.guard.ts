import { CanDeactivateFn } from '@angular/router';
import type { PhotographersPage } from './pages/photographers/photographers-page';

export const photographerDraftUnsavedGuard: CanDeactivateFn<PhotographersPage> = (page) =>
  page.canLeave();
