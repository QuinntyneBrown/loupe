import { CanDeactivateFn } from '@angular/router';
import type { LocationDetailPage } from './pages/location-detail/location-detail-page';

export const locationUnsavedGuard: CanDeactivateFn<LocationDetailPage> = (page) => page.canLeave();
