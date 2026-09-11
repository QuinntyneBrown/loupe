import { InjectionToken } from '@angular/core';
import { OperationResult } from '../operation/operation-result';
import { PhotographerResult } from '../photographer/photographer-metadata';
import { PhotographerSuggestions } from './photographer-suggestions';
import { PhotographerSuggestionReview } from './photographer-suggestion-review';

export interface IPhotographerSummaryService {
  current(id: string): Promise<OperationResult | null>;
  suggestions(id: string): Promise<PhotographerSuggestions | null>;
  request(id: string, revision: number, operationKey: string): Promise<OperationResult>;
  review(id: string, decision: PhotographerSuggestionReview): Promise<PhotographerResult>;
  undo(id: string, revision: number): Promise<PhotographerResult>;
}
export const PHOTOGRAPHER_SUMMARY_SERVICE = new InjectionToken<IPhotographerSummaryService>(
  'PHOTOGRAPHER_SUMMARY_SERVICE',
);
