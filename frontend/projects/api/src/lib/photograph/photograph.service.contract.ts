import { InjectionToken } from '@angular/core';
import { PhotographPage } from './photograph-page';
import { PhotographResult } from './photograph-result';
import { CritiqueBrief } from './critique-brief';
import { PhotographUpload } from './photograph-upload';
import { UploadProgress } from '../common/upload-progress';

export interface IPhotographService {
  upload(
    input: PhotographUpload,
    onProgress?: (progress: UploadProgress) => void,
  ): Promise<PhotographResult>;
  list(cursor?: string): Promise<PhotographPage>;
  get(id: string): Promise<PhotographResult>;
  updateNotes(id: string, revision: number, notes: string): Promise<PhotographResult>;
  updateBrief(id: string, revision: number, brief: CritiqueBrief): Promise<PhotographResult>;
}

export const PHOTOGRAPH_SERVICE = new InjectionToken<IPhotographService>('Photograph service');
