import { OperationResult } from '../operation/operation-result';

export interface ReferenceDraftResult {
  id: string;
  title: string;
  sourceUrl: string | null;
  attribution: string | null;
  width: number | null;
  height: number | null;
  imageUrl: string | null;
  previewUrl: string | null;
  revision: number;
  expiresAt: string;
  committedReferenceId: string | null;
  failureCode: string | null;
  import: OperationResult | null;
}
