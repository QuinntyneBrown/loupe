import { ReferenceMetadata } from 'api';

export const referenceMetadataFields = [
  { key: 'title', label: 'Title (optional)', maximum: 200 },
  { key: 'sourceUrl', label: 'Source URL (optional)', maximum: 2048 },
  { key: 'attribution', label: 'Attribution (optional)', maximum: 200 },
  { key: 'notes', label: 'Notes (optional)', maximum: 10000 },
] as const;

export function normalizeReferenceMetadata(value: ReferenceMetadata): ReferenceMetadata {
  const clean = (text: string | null) => text?.replace(/\r\n?/g, '\n').trim() || null;
  return {
    title: clean(value.title) ?? '',
    sourceUrl: clean(value.sourceUrl),
    attribution: clean(value.attribution),
    notes: clean(value.notes),
  };
}

export function referenceMetadataErrors(
  value: ReferenceMetadata,
  requireTitle: boolean,
): Record<string, string> {
  const errors: Record<string, string> = {};
  const normalized = normalizeReferenceMetadata(value);
  for (const field of referenceMetadataFields) {
    if ([...(normalized[field.key] ?? '')].length > field.maximum)
      errors[field.key] = `Use ${field.maximum.toLocaleString('en-US')} characters or fewer.`;
  }
  if (requireTitle && !normalized.title) errors['title'] = 'Enter a title.';
  const source = normalized.sourceUrl;
  if (source && !errors['sourceUrl']) {
    try {
      const url = new URL(source);
      if (
        !/^https?:\/\//i.test(source) ||
        /[\s\\]/.test(source) ||
        url.username ||
        url.password ||
        url.port
      )
        throw new Error('invalid_source');
    } catch {
      errors['sourceUrl'] = 'Use an HTTP or HTTPS URL without credentials or a nonstandard port.';
    }
  }
  return errors;
}
