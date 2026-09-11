import { ReferenceSummary } from '../reference/reference-result';
import { ReferenceTag } from '../reference/reference-tag';

export interface PhotographerSummary {
  id: string;
  name: string;
  portfolioUrl: string;
  createdAt: string;
  summary: string | null;
  tags: ReferenceTag[];
  referenceCount: number;
  references: ReferenceSummary[];
}
export interface PhotographerPage {
  items: PhotographerSummary[];
  nextCursor: string | null;
  totalCount: number;
}
