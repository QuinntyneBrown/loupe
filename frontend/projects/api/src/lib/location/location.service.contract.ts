import { InjectionToken } from '@angular/core';
import { LocationPage, LocationResult } from './location-result';
import { OperationResult } from '../operation/operation-result';
import { LocationDetailsInput, LocationInput, LocationTagInput } from './location-input';
import { UploadProgress } from '../common/upload-progress';

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
  addImage(
    id: string,
    image: File,
    operationKey: string,
    onProgress?: (progress: UploadProgress) => void,
    signal?: AbortSignal,
  ): Promise<LocationResult>;
  removeImage(id: string, imageId: string, revision: number): Promise<LocationResult>;
  setCover(id: string, imageId: string, revision: number): Promise<LocationResult>;
  /** Re-queues a failed search index run through the shared operations route. */
  retryIndex(operationId: string, revision: number, operationKey: string): Promise<OperationResult>;
}
export const LOCATION_SERVICE = new InjectionToken<ILocationService>('LOCATION_SERVICE');
