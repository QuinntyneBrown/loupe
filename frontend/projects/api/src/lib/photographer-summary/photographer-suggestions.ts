import { PhotographerResult } from '../photographer/photographer-metadata';

export interface PhotographerSuggestedTag {
  name: string;
  category: string;
  state: 'pending' | 'accepted' | 'dismissed';
}
export interface PhotographerSuggestions {
  operationId: string;
  sourceRevision: number;
  createdAt: string;
  mode: 'Live' | 'Demo';
  model: string;
  promptVersion: string;
  source: NonNullable<PhotographerResult['source']>;
  summary: string | null;
  summaryStatus: 'pending' | 'accepted' | 'dismissed' | 'unavailable';
  tags: PhotographerSuggestedTag[];
  unavailableReason: string | null;
}
