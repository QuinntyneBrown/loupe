import { Injectable } from '@angular/core';
import {
  ILocationService,
  LocationDetailsInput,
  LocationInput,
  LocationPage,
  LocationResult,
  LocationTagInput,
  LocationTextField,
  ServiceError,
} from 'api';

@Injectable()
export class MockLocationService implements ILocationService {
  list(cursor?: string): Promise<LocationPage> {
    return this.call('list', { cursor });
  }
  get(id: string): Promise<LocationResult> {
    return this.call('get', { id });
  }
  create(input: LocationInput, operationKey: string): Promise<LocationResult> {
    return this.call('create', { ...input, operationKey });
  }
  update(id: string, input: LocationDetailsInput & { revision: number }): Promise<LocationResult> {
    return this.call('update', { id, ...input });
  }
  updateText(
    id: string,
    revision: number,
    field: LocationTextField,
    text: string | null,
  ): Promise<LocationResult> {
    return this.call('updateText', { id, revision, field, text });
  }
  setTags(id: string, revision: number, tags: LocationTagInput[]): Promise<LocationResult> {
    return this.call('setTags', { id, revision, tags });
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
