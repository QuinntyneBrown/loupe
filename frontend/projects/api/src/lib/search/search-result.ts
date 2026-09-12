export interface SearchItem {
  id: string;
  type: 'reference' | 'photographer';
  title: string;
  createdAt: string;
  previewUrl: string | null;
  sourceUrl: string | null;
  attribution: string | null;
  description: string | null;
  width: number | null;
  height: number | null;
  referenceCount: number;
  referencePreviewUrls: string[];
}
export interface SearchResult {
  items: SearchItem[];
  nextCursor: string | null;
  totalCount: number;
}
