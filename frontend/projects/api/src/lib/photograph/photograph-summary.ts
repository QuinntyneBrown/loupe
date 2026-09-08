import { OperationResult } from '../operation/operation-result';

export interface PhotographSummary {
  id: string;
  title: string;
  createdAt: string;
  width: number;
  height: number;
  previewUrl: string;
  hasCritique: boolean;
  critiqueStatus: OperationResult['status'] | null;
}
