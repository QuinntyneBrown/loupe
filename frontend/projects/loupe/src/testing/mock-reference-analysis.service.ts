import { Injectable } from '@angular/core';
import {
  IReferenceAnalysisService,
  OperationResult,
  ReferenceResult,
  ReferenceSuggestions,
  ReferenceSuggestionReview,
  ServiceError,
} from 'api';

@Injectable()
export class MockReferenceAnalysisService implements IReferenceAnalysisService {
  current(id: string): Promise<OperationResult | null> {
    return this.call('current', { id });
  }
  suggestions(id: string): Promise<ReferenceSuggestions | null> {
    return this.call('suggestions', { id });
  }
  request(id: string, revision: number, operationKey: string): Promise<OperationResult> {
    return this.call('request', { id, revision, operationKey });
  }
  review(id: string, decision: ReferenceSuggestionReview): Promise<ReferenceResult> {
    return this.call('review', { id, ...decision });
  }
  undo(id: string, revision: number): Promise<ReferenceResult> {
    return this.call('undo', { id, revision });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeReferenceAnalysis?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupeReferenceAnalysis;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data as T;
  }
}
