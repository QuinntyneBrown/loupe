import { InjectionToken } from '@angular/core';
import { SearchRequest } from './search-request';
import { SearchResult } from './search-result';
import { SearchTagFacet } from './search-tag-facet';

export interface ISearchService {
  search(request: SearchRequest): Promise<SearchResult>;
  tags(selectedTags?: string[]): Promise<SearchTagFacet[]>;
}
export const SEARCH_SERVICE = new InjectionToken<ISearchService>('SEARCH_SERVICE');
