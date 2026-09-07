import { PhotographSummary } from './photograph-summary';
import { CritiqueBrief } from './critique-brief';
import { CaptureMetadata } from './capture-metadata';

export interface PhotographResult extends PhotographSummary {
  imageUrl: string;
  brief: CritiqueBrief;
  exif: CaptureMetadata;
  revision: number;
  notes: string | null;
}
