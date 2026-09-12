import { InjectionToken } from '@angular/core';
import { OperationResult } from '../operation/operation-result';
import { SavedScoutingReport } from './scouting-report-result';

export interface IScoutingReportService {
  current(locationId: string): Promise<OperationResult | null>;
  get(locationId: string): Promise<SavedScoutingReport | null>;
  request(
    locationId: string,
    revision: number,
    regenerate: boolean,
    operationKey: string,
  ): Promise<OperationResult>;
  retry(operationId: string, revision: number, operationKey: string): Promise<OperationResult>;
}
export const SCOUTING_REPORT_SERVICE = new InjectionToken<IScoutingReportService>(
  'SCOUTING_REPORT_SERVICE',
);
