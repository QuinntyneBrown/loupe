import { InjectionToken } from '@angular/core';
import { PhotographerPage } from './photographer-result';
import { PhotographerResult, PhotographerMetadata } from './photographer-metadata';
import { ReferenceSummary } from '../reference/reference-result';
import { ReferenceCandidatePage } from './reference-candidate';

export interface PhotographerReferencePage {
  items: ReferenceSummary[];
  nextCursor: string | null;
  totalCount: number;
}

export interface IPhotographerService {
  candidates(id: string, query: string, cursor?: string): Promise<ReferenceCandidatePage>;
  list(cursor?: string, query?: string): Promise<PhotographerPage>;
  get(id: string): Promise<PhotographerResult>;
  update(
    id: string,
    input: PhotographerMetadata & { revision: number },
  ): Promise<PhotographerResult>;
  references(id: string, cursor?: string): Promise<PhotographerReferencePage>;
}
export const PHOTOGRAPHER_SERVICE = new InjectionToken<IPhotographerService>(
  'PHOTOGRAPHER_SERVICE',
);
