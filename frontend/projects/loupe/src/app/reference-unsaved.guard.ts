import { CanDeactivateFn } from '@angular/router';
import { ReferenceDetailPage } from './pages/reference-detail/reference-detail-page';
export const referenceUnsavedGuard: CanDeactivateFn<ReferenceDetailPage> = (page) =>
  page.canLeave();
