export interface ReferenceSuggestionReview {
  operationId: string;
  revision: number;
  target: 'description' | 'tag' | 'all';
  decision: 'accept' | 'dismiss';
  name?: string;
  value?: string;
  category?: string;
}
