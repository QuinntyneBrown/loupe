import { InjectionToken } from '@angular/core';
import { OperationResult } from '../operation/operation-result';
import { ReferenceImportRequest } from './reference-import-request';
export interface IReferenceImportService {
  current(referenceId: string): Promise<OperationResult | null>;
  request(referenceId: string, request: ReferenceImportRequest): Promise<OperationResult>;
}
export const REFERENCE_IMPORT_SERVICE = new InjectionToken<IReferenceImportService>(
  'REFERENCE_IMPORT_SERVICE',
);
