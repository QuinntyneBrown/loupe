import { ReferenceMetadata } from './reference-metadata';
import { ReferenceResult } from './reference-result';
export interface ReferenceLink extends ReferenceMetadata {
  operationKey: string;
}
export interface ReferenceLinkResult {
  reference: ReferenceResult;
  alreadySaved: boolean;
}
