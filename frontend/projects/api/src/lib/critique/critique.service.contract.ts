import { InjectionToken } from '@angular/core';
import { SavedCritique } from './critique-result';

export interface ICritiqueService {
  get(photographId: string): Promise<SavedCritique | null>;
}
export const CRITIQUE_SERVICE = new InjectionToken<ICritiqueService>('Critique service');
