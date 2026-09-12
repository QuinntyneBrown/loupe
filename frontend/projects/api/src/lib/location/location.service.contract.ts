import { InjectionToken } from '@angular/core';
import { LocationPage } from './location-result';

export interface ILocationService {
  list(cursor?: string): Promise<LocationPage>;
}
export const LOCATION_SERVICE = new InjectionToken<ILocationService>('LOCATION_SERVICE');
