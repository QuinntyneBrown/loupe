export interface ReferenceSummary {
  id: string;
  title: string;
  createdAt: string;
  width: number | null;
  height: number | null;
  previewUrl: string | null;
  sourceUrl: string | null;
  attribution: string | null;
}
export interface ReferenceResult extends ReferenceSummary {
  photographer: { id: string; name: string; portfolioUrl: string } | null;
  description: string | null;
  descriptionProvenance: 'manual' | 'ai-accepted' | 'edited-ai' | null;
  tags: ReferenceTag[];
  boardIds: string[];
  notes: string | null;
  imageUrl: string | null;
  revision: number;
}
export interface ReferencePage {
  totalCount: number;
  libraryCount: number;
  items: ReferenceSummary[];
  nextCursor: string | null;
}
import { ReferenceTag } from './reference-tag';
