import { Injectable } from '@angular/core';
import { ILocationService, LocationInput, LocationPage, LocationResult, ServiceError } from 'api';

@Injectable()
export class MockLocationService implements ILocationService {
  list(cursor?: string): Promise<LocationPage> {
    return this.call('list', { cursor });
  }
  create(input: LocationInput, operationKey: string): Promise<LocationResult> {
    return this.call('create', { ...input, operationKey });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeLocations?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string; errors?: Record<string, string[]> }>;
      }
    ).loupeLocations;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error, result.errors);
    return result.data as T;
  }
}
