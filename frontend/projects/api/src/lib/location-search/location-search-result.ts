import { LocationReportStatus } from '../location/location-result';
import { ShootType, SuitabilityRating, TimeOfDay } from '../scouting-report/scouting-report-result';

export interface LocationSearchGroupSize {
  cannotAssess: boolean;
  minimum: number | null;
  maximum: number | null;
}
export interface LocationSearchSuitability {
  shootType: ShootType;
  rating: SuitabilityRating;
}
export interface LocationSearchItem {
  id: string;
  name: string;
  locality: string | null;
  coverPreviewUrl: string | null;
  imageCount: number;
  reportStatus: LocationReportStatus;
  recommendedPeriods: TimeOfDay[];
  groupSize: LocationSearchGroupSize | null;
  suitability: LocationSearchSuitability[];
  createdAt: string;
}
export interface LocationSearchPage {
  items: LocationSearchItem[];
  nextCursor: string | null;
  totalCount: number;
}
export interface LocationTagFacet {
  name: string;
  count: number;
  normalizedName: string;
}
