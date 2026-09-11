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
  sourceRevision: number;
  sourceIsCurrent: boolean;
  sourceFailureCode: string | null;
  source: {
    requestedUrl: string;
    fetchedUrl: string;
    retrievedAt: string;
    title: string | null;
    description: string | null;
    mainText: string;
    tags: string[];
  } | null;
}
export interface SavePhotographerResult {
  photographer: PhotographerResult;
  alreadySaved: boolean;
}
