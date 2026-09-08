import { PhotographResult } from '../photograph/photograph-result';
import { SavedCritique } from '../critique/critique-result';

export interface ComparisonAttempt {
  photograph: PhotographResult;
  critique: SavedCritique;
}
export interface ComparisonResult {
  first: ComparisonAttempt;
  second: ComparisonAttempt;
}
