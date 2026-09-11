import { InjectionToken } from '@angular/core';
import { PhotographerPage } from './photographer-result';

export interface IPhotographerService {
  list(cursor?: string): Promise<PhotographerPage>;
}
export const PHOTOGRAPHER_SERVICE = new InjectionToken<IPhotographerService>(
  'PHOTOGRAPHER_SERVICE',
);
