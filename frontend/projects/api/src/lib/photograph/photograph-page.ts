import { PhotographSummary } from './photograph-summary';

export interface PhotographPage {
  items: PhotographSummary[];
  nextCursor: string | null;
}
