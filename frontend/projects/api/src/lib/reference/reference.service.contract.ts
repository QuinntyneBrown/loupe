import { ReferenceUpload } from './reference-upload';
import { UploadProgress } from '../common/upload-progress';
import { InjectionToken } from '@angular/core';
import { ReferencePage, ReferenceResult } from './reference-result';

export interface IReferenceService {
  upload(
    input: ReferenceUpload,
    onProgress?: (progress: UploadProgress) => void,
  ): Promise<ReferenceResult>;
  list(cursor?: string): Promise<ReferencePage>;
  get(id: string): Promise<ReferenceResult>;
}
export const REFERENCE_SERVICE = new InjectionToken<IReferenceService>('REFERENCE_SERVICE');
