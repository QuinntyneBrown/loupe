import { CanDeactivateFn } from '@angular/router';
import type { LocationsPage } from './pages/locations/locations-page';

export const locationsUnsavedGuard: CanDeactivateFn<LocationsPage> = (page) => page.canLeave();
