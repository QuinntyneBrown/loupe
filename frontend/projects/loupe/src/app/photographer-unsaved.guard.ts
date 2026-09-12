import { CanDeactivateFn } from '@angular/router';
import type { PhotographerDetailPage } from './pages/photographer-detail/photographer-detail-page';
export const photographerUnsavedGuard: CanDeactivateFn<PhotographerDetailPage> = (page) =>
  page.canLeave();
