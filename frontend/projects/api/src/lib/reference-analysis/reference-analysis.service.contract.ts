import { InjectionToken } from '@angular/core';
import { OperationResult } from '../operation/operation-result';
import { ReferenceResult } from '../reference/reference-result';
import { ReferenceSuggestions } from './reference-suggestions';
import { ReferenceSuggestionReview } from './reference-suggestion-review';

export interface IReferenceAnalysisService {
  current(id: string): Promise<OperationResult | null>;
  suggestions(id: string): Promise<ReferenceSuggestions | null>;
  request(id: string, revision: number, operationKey: string): Promise<OperationResult>;
  review(id: string, decision: ReferenceSuggestionReview): Promise<ReferenceResult>;
  undo(id: string, revision: number): Promise<ReferenceResult>;
}
export const REFERENCE_ANALYSIS_SERVICE = new InjectionToken<IReferenceAnalysisService>(
  'REFERENCE_ANALYSIS_SERVICE',
);
