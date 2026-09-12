import { Injectable } from '@angular/core';
import {
  ILocationSearchService,
  LocationSearchPage,
  LocationSearchRequest,
  LocationTagFacet,
  ServiceError,
} from 'api';

@Injectable()
export class MockLocationSearchService implements ILocationSearchService {
  search(request: LocationSearchRequest): Promise<LocationSearchPage> {
    return this.call('search', request);
  }
  tags(): Promise<LocationTagFacet[]> {
    return this.call('tags', {});
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeLocationSearch?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string; errors?: Record<string, string[]> }>;
      }
    ).loupeLocationSearch;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error, result.errors);
    return result.data as T;
  }
}
