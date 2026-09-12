import { SearchRequest } from 'api';

export function isSearchType(value: string): value is SearchRequest['type'] {
  return value === 'all' || value === 'references' || value === 'photographers';
}

export function isSearchTag(value: string): boolean {
  const length = Array.from(value.normalize('NFC').trim()).length;
  return length > 0 && length <= 50 && !value.includes('\0');
}

export function isSearchBoardId(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}
