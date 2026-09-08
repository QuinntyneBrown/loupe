import { Injectable } from '@angular/core';
import {
  CritiqueRequest,
  CritiqueRetry,
  ICritiqueService,
  OperationResult,
  SavedCritique,
  ServiceError,
} from 'api';

@Injectable()
export class MockCritiqueService implements ICritiqueService {
  async retry(operationId: string, request: CritiqueRetry): Promise<OperationResult> {
    return this.call('retryCritique', { operationId, ...request }) as Promise<OperationResult>;
  }
  async get(id: string): Promise<SavedCritique | null> {
    return this.call('getCritique', { id }) as Promise<SavedCritique | null>;
  }
  async getOperation(id: string): Promise<OperationResult | null> {
    return this.call('getCritiqueOperation', { id }) as Promise<OperationResult | null>;
  }
  async request(id: string, request: CritiqueRequest): Promise<OperationResult> {
    return this.call('requestCritique', { id, ...request }) as Promise<OperationResult>;
  }
  private async call(operation: string, input: object): Promise<unknown> {
    const callback = (
      window as Window & {
        loupePhotographs?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupePhotographs;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data;
  }
}
