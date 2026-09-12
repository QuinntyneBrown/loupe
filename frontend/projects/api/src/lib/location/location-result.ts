import { SavedScoutingReport } from '../scouting-report/scouting-report-result';

export type LocationSetting = 'Indoor' | 'Outdoor' | 'Mixed';
export type LocationReportStatus = 'None' | 'Queued' | 'Running' | 'Ready' | 'Outdated' | 'Failed';

export interface Coordinates {
  latitude: string;
  longitude: string;
}
export interface LocationTag {
  name: string;
  category: string | null;
}
export interface LocationImage {
  id: string;
  position: number;
  imageUrl: string;
  previewUrl: string;
  width: number;
  height: number;
}
export interface LocationResult {
  id: string;
  name: string;
  addressLine1: string | null;
  addressLine2: string | null;
  locality: string | null;
  region: string | null;
  postalCode: string | null;
  country: string | null;
  coordinates: Coordinates | null;
  setting: LocationSetting | null;
  scoutingBrief: string | null;
  notes: string | null;
  tags: LocationTag[];
  images: LocationImage[];
  coverImageId: string | null;
  report: SavedScoutingReport | null;
  reportStatus: LocationReportStatus;
  createdAt: string;
  updatedAt: string;
  revision: number;
}
export interface LocationSummary {
  id: string;
  name: string;
  locality: string | null;
  coverPreviewUrl: string | null;
  imageCount: number;
  reportStatus: LocationReportStatus;
  createdAt: string;
}
export interface LocationPage {
  items: LocationSummary[];
  nextCursor: string | null;
  totalCount: number;
}
