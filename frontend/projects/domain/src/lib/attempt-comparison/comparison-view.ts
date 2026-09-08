import { ComparisonAttempt } from 'api';

export interface ComparisonView {
  first: ComparisonAttempt | null;
  second: ComparisonAttempt | null;
}
