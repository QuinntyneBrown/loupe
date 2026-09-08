import { InjectionToken } from '@angular/core';
import { ComparisonResult } from './comparison-result';

export interface IComparisonService {
  load(firstId: string, secondId: string): Promise<ComparisonResult>;
}
export const COMPARISON_SERVICE = new InjectionToken<IComparisonService>('COMPARISON_SERVICE');
