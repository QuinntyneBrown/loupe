export interface PhotographerSuggestionReview {
  operationId: string;
  revision: number;
  target: 'summary' | 'tag' | 'all';
  decision: 'accept' | 'dismiss';
  name?: string;
  value?: string;
  category?: string;
}
