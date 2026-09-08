import { InjectionToken } from '@angular/core';
import { SavedCritique } from './critique-result';
import { OperationResult } from '../operation/operation-result';

export interface ICritiqueService {
  get(photographId: string): Promise<SavedCritique | null>;
  getOperation(photographId: string): Promise<OperationResult | null>;
}
export const CRITIQUE_SERVICE = new InjectionToken<ICritiqueService>('Critique service');
