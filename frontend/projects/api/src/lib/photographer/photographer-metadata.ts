import { ReferenceTag } from '../reference/reference-tag';

export interface PhotographerMetadata {
  name: string;
  portfolioUrl: string;
  summary: string | null;
  notes: string | null;
  tags: Pick<ReferenceTag, 'name' | 'category'>[];
}
export interface PhotographerResult extends PhotographerMetadata {
  id: string;
  createdAt: string;
  revision: number;
  summaryProvenance: string | null;
  tags: ReferenceTag[];
}
export interface SavePhotographerResult {
  photographer: PhotographerResult;
  alreadySaved: boolean;
}
