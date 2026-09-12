import { InjectionToken } from '@angular/core';
import { LocationPage, LocationResult } from './location-result';
import { LocationDetailsInput, LocationInput, LocationTagInput } from './location-input';

export type LocationTextField = 'scoutingBrief' | 'notes';

export interface ILocationService {
  list(cursor?: string): Promise<LocationPage>;
  get(id: string): Promise<LocationResult>;
  create(input: LocationInput, operationKey: string): Promise<LocationResult>;
  update(id: string, input: LocationDetailsInput & { revision: number }): Promise<LocationResult>;
  updateText(
    id: string,
    revision: number,
    field: LocationTextField,
    text: string | null,
  ): Promise<LocationResult>;
  setTags(id: string, revision: number, tags: LocationTagInput[]): Promise<LocationResult>;
}
export const LOCATION_SERVICE = new InjectionToken<ILocationService>('LOCATION_SERVICE');
