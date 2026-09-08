import { InjectionToken } from '@angular/core';
import { ComparisonResult } from './comparison-result';
import { PhotographPage } from '../photograph/photograph-page';

export interface IComparisonService {
  eligible(cursor?: string): Promise<PhotographPage>;
  load(firstId: string, secondId: string): Promise<ComparisonResult>;
}
export const COMPARISON_SERVICE = new InjectionToken<IComparisonService>('COMPARISON_SERVICE');
