import { InjectionToken } from '@angular/core';
import { LocationSearchRequest } from './location-search-request';
import { LocationSearchPage, LocationTagFacet } from './location-search-result';

export interface ILocationSearchService {
  search(request: LocationSearchRequest): Promise<LocationSearchPage>;
  tags(): Promise<LocationTagFacet[]>;
}
export const LOCATION_SEARCH_SERVICE = new InjectionToken<ILocationSearchService>(
  'LOCATION_SEARCH_SERVICE',
);
