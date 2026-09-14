import { Injectable } from '@angular/core';
import { IScoutingReportService, OperationResult, SavedScoutingReport, ServiceError } from 'api';

@Injectable()
export class MockScoutingReportService implements IScoutingReportService {
  current(locationId: string): Promise<OperationResult | null> {
    return this.call('current', { id: locationId });
  }
  get(locationId: string): Promise<SavedScoutingReport | null> {
    return this.call('get', { id: locationId });
  }
  request(
    locationId: string,
    revision: number,
    regenerate: boolean,
    operationKey: string,
  ): Promise<OperationResult> {
    return this.call('request', { id: locationId, revision, regenerate, operationKey });
  }
  retry(operationId: string, revision: number, operationKey: string): Promise<OperationResult> {
    return this.call('retry', { operationId, revision, operationKey });
  }
  private async call<T>(operation: string, input: object): Promise<T> {
    const callback = (
      window as Window & {
        loupeScoutingReports?: (
          operation: string,
          input: object,
        ) => Promise<{ data?: unknown; error?: string; errors?: Record<string, string[]> }>;
      }
    ).loupeScoutingReports;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(operation, input);
    if (result.error) throw new ServiceError(result.error, result.errors);
    return result.data as T;
  }
}
