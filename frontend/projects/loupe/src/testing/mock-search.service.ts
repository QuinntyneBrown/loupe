import { Injectable } from '@angular/core';
import { ISearchService, SearchRequest, SearchResult, SearchTagFacet, ServiceError } from 'api';

@Injectable()
export class MockSearchService implements ISearchService {
  async tags(selectedTags: string[] = []): Promise<SearchTagFacet[]> {
    const callback = (
      window as Window & {
        loupeSearchTags?: (selectedTags: string[]) => Promise<{
          data?: SearchTagFacet[];
          error?: string;
          errors?: Record<string, string[]>;
        }>;
      }
    ).loupeSearchTags;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(selectedTags);
    if (result.error || !result.data)
      throw new ServiceError(result.error ?? 'request_failed', result.errors);
    return result.data;
  }
  async search(request: SearchRequest): Promise<SearchResult> {
    const callback = (
      window as Window & {
        loupeSearch?: (
          input: SearchRequest,
        ) => Promise<{ data?: SearchResult; error?: string; errors?: Record<string, string[]> }>;
      }
    ).loupeSearch;
    if (!callback) throw new ServiceError('request_failed');
    const result = await callback(request);
    if (result.error || !result.data)
      throw new ServiceError(result.error ?? 'request_failed', result.errors);
    return result.data;
  }
}
