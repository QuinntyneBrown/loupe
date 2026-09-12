import { InjectionToken } from '@angular/core';
import { SavePhotographerResult } from '../photographer/photographer-metadata';
import { PhotographerDraftResult, SavePhotographerDraftInput } from './photographer-draft-result';

export interface IPhotographerDraftService {
  import(
    portfolioUrl: string,
    name: string | null,
    operationKey: string,
  ): Promise<PhotographerDraftResult>;
  get(id: string): Promise<PhotographerDraftResult>;
  cancel(id: string): Promise<void>;
  save(
    id: string,
    input: SavePhotographerDraftInput,
    operationKey: string,
  ): Promise<SavePhotographerResult>;
}
export const PHOTOGRAPHER_DRAFT_SERVICE = new InjectionToken<IPhotographerDraftService>(
  'PHOTOGRAPHER_DRAFT_SERVICE',
);
