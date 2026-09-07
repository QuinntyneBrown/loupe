import { InjectionToken } from '@angular/core';
import { DeletionResult } from './deletion-result';

export interface IDeletionService {
  deletePhotograph(id: string, revision: number): Promise<DeletionResult>;
  get(id: string): Promise<DeletionResult>;
}

export const DELETION_SERVICE = new InjectionToken<IDeletionService>('Deletion service');
