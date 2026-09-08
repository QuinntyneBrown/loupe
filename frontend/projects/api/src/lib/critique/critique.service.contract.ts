import { InjectionToken } from '@angular/core';
import { SavedCritique } from './critique-result';
import { OperationResult } from '../operation/operation-result';
import { CritiqueRequest } from './critique-request';
import { CritiqueRetry } from './critique-retry';

export interface ICritiqueService {
  get(photographId: string): Promise<SavedCritique | null>;
  getOperation(photographId: string): Promise<OperationResult | null>;
  request(photographId: string, request: CritiqueRequest): Promise<OperationResult>;
  retry(operationId: string, request: CritiqueRetry): Promise<OperationResult>;
}
export const CRITIQUE_SERVICE = new InjectionToken<ICritiqueService>('Critique service');
