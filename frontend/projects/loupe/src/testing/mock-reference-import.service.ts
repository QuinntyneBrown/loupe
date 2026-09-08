import { Injectable } from '@angular/core';
import {
  IReferenceImportService,
  ReferenceImportRequest,
  OperationResult,
  ServiceError,
} from 'api';
@Injectable()
export class MockReferenceImportService implements IReferenceImportService {
  current(id: string): Promise<OperationResult | null> {
    return this.call('current', { id });
  }
  request(id: string, request: ReferenceImportRequest): Promise<OperationResult> {
    return this.call('request', { id, ...request });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeReferenceImports?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string }>;
      }
    ).loupeReferenceImports;
    if (!callback) throw new ServiceError('item_unavailable');
    const response = await callback(operation, input);
    if (response.error) throw new ServiceError(response.error);
    return response.data as T;
  }
}
