import { InjectionToken } from '@angular/core';
import { PhotographPage } from './photograph-page';
import { PhotographResult } from './photograph-result';

export interface IPhotographService {
  list(cursor?: string): Promise<PhotographPage>;
  get(id: string): Promise<PhotographResult>;
  updateNotes(id: string, revision: number, notes: string): Promise<PhotographResult>;
}

export const PHOTOGRAPH_SERVICE = new InjectionToken<IPhotographService>('Photograph service');
