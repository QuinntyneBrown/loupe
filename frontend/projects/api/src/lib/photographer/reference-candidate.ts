import { ReferenceResult } from '../reference/reference-result';
export type ReferenceCandidate = Pick<
  ReferenceResult,
  'id' | 'title' | 'createdAt' | 'previewUrl' | 'revision' | 'photographer'
>;
export interface ReferenceCandidatePage {
  items: ReferenceCandidate[];
  nextCursor: string | null;
  totalCount: number;
}
