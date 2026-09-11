import { InjectionToken } from '@angular/core';
import { ReferenceDraftResult } from './reference-draft-result';
import { ReferenceMetadata } from '../reference/reference-metadata';
import { ReferenceLinkResult } from '../reference/reference-link';
import { UploadProgress } from '../common/upload-progress';

export interface IReferenceDraftService {
  upload(
    image: File,
    sourceUrl: string,
    operationKey: string,
    progress?: (value: UploadProgress) => void,
  ): Promise<ReferenceDraftResult>;
  get(id: string): Promise<ReferenceDraftResult>;
  cancel(id: string): Promise<void>;
  save(
    id: string,
    revision: number,
    metadata: ReferenceMetadata,
    boardIds: string[],
    operationKey: string,
  ): Promise<ReferenceLinkResult>;
}
export const REFERENCE_DRAFT_SERVICE = new InjectionToken<IReferenceDraftService>(
  'REFERENCE_DRAFT_SERVICE',
);
