import { InjectionToken } from '@angular/core';
import { ReferencePage, ReferenceResult } from './reference-result';

export interface IReferenceService {
  list(cursor?: string): Promise<ReferencePage>;
  get(id: string): Promise<ReferenceResult>;
}
export const REFERENCE_SERVICE = new InjectionToken<IReferenceService>('REFERENCE_SERVICE');
