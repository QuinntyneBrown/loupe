export interface ReferenceSuggestedTag {
  name: string;
  category: string;
  state: 'pending' | 'accepted' | 'dismissed';
}
export interface ReferenceSuggestions {
  operationId: string;
  imageRevision: number;
  createdAt: string;
  mode: 'Live' | 'Demo';
  model: string;
  promptVersion: string;
  description: string | null;
  descriptionState: 'pending' | 'accepted' | 'dismissed';
  tags: ReferenceSuggestedTag[];
}
