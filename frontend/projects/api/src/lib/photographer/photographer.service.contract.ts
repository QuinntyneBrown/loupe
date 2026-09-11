import { InjectionToken } from '@angular/core';
import { PhotographerPage } from './photographer-result';
import { PhotographerResult } from './photographer-metadata';
import { ReferenceSummary } from '../reference/reference-result';

export interface PhotographerReferencePage {
  items: ReferenceSummary[];
  nextCursor: string | null;
  totalCount: number;
}

export interface IPhotographerService {
  list(cursor?: string): Promise<PhotographerPage>;
  get(id: string): Promise<PhotographerResult>;
  references(id: string, cursor?: string): Promise<PhotographerReferencePage>;
}
export const PHOTOGRAPHER_SERVICE = new InjectionToken<IPhotographerService>(
  'PHOTOGRAPHER_SERVICE',
);
