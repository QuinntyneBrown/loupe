import { ReferenceLink, ReferenceLinkResult } from './reference-link';
import { ReferenceMetadata } from './reference-metadata';
import { ReferenceUpload } from './reference-upload';
import { UploadProgress } from '../common/upload-progress';
import { InjectionToken } from '@angular/core';
import { ReferencePage, ReferenceResult } from './reference-result';
import { ReferenceTag, ReferenceTagFacet } from './reference-tag';

export interface IReferenceService {
  replaceImage(
    id: string,
    revision: number,
    image: File,
    operationKey: string,
    onProgress?: (progress: UploadProgress) => void,
  ): Promise<ReferenceResult>;
  setTags(
    id: string,
    revision: number,
    tags: Pick<ReferenceTag, 'name' | 'category'>[],
  ): Promise<ReferenceResult>;
  saveLink(input: ReferenceLink): Promise<ReferenceLinkResult>;
  upload(
    input: ReferenceUpload,
    onProgress?: (progress: UploadProgress) => void,
  ): Promise<ReferenceResult>;
  update(id: string, revision: number, metadata: ReferenceMetadata): Promise<ReferenceResult>;
  list(cursor?: string, boardId?: string, tags?: string[]): Promise<ReferencePage>;
  tags(boardId?: string): Promise<ReferenceTagFacet[]>;
  get(id: string): Promise<ReferenceResult>;
}
export const REFERENCE_SERVICE = new InjectionToken<IReferenceService>('REFERENCE_SERVICE');
