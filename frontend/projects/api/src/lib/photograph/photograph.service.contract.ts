import { InjectionToken } from '@angular/core';
import { PhotographPage } from './photograph-page';

export interface IPhotographService {
  list(cursor?: string): Promise<PhotographPage>;
}

export const PHOTOGRAPH_SERVICE = new InjectionToken<IPhotographService>('Photograph service');
