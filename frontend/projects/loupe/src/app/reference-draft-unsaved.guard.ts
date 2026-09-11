import { CanDeactivateFn } from '@angular/router';
import type { InspirationPage } from './pages/inspiration/inspiration-page';

export const referenceDraftUnsavedGuard: CanDeactivateFn<InspirationPage> = (page) =>
  page.canLeave();
