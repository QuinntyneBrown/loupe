import { CanDeactivateFn } from '@angular/router';
import { PhotographDetailPage } from './pages/photograph-detail/photograph-detail-page';

export const photographUnsavedGuard: CanDeactivateFn<PhotographDetailPage> = (page) =>
  page.canLeave();
