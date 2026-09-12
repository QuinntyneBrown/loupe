import { Injectable } from '@angular/core';
import { ILocationService, LocationPage, ServiceError } from 'api';

@Injectable()
export class MockLocationService implements ILocationService {
  list(cursor?: string): Promise<LocationPage> {
    return this.call('list', { cursor });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeLocations?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupeLocations;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error);
    return result.data as T;
  }
}
