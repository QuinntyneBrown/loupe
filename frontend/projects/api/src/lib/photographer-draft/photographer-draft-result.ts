import { OperationResult } from '../operation/operation-result';
import { PhotographerMetadata } from '../photographer/photographer-metadata';

export interface PhotographerDraftResult {
  id: string;
  portfolioUrl: string;
  name: string | null;
  description: string | null;
  tags: string[];
  revision: number;
  expiresAt: string;
  committedPhotographerId: string | null;
  failureCode: string | null;
  import: OperationResult | null;
}
export interface SavePhotographerDraftInput extends PhotographerMetadata {
  revision: number;
}
