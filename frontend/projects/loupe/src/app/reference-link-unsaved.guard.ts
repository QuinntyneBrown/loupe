import { CanDeactivateFn } from '@angular/router';
import { ReferenceLinkPage } from './pages/reference-link/reference-link-page';
export const referenceLinkUnsavedGuard: CanDeactivateFn<ReferenceLinkPage> = (page) =>
  page.canLeave();
