import { PhotographResult } from 'api';

export interface UploadCompletion {
  photograph: PhotographResult;
  critique: 'not-requested' | 'requested' | 'unconfirmed';
}
