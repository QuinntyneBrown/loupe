export interface SearchRequest {
  query: string;
  type: 'all' | 'references' | 'photographers';
  tags: string[];
  boardIds: string[];
  mode?: 'keyword';
  cursor?: string;
}
