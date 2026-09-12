import { InjectionToken } from '@angular/core';
import { LocationPage, LocationResult } from './location-result';
import { LocationInput } from './location-input';

export interface ILocationService {
  list(cursor?: string): Promise<LocationPage>;
  create(input: LocationInput, operationKey: string): Promise<LocationResult>;
}
export const LOCATION_SERVICE = new InjectionToken<ILocationService>('LOCATION_SERVICE');
