import { LocationSetting } from '../location/location-result';
import { ShootType, TimeOfDay } from '../scouting-report/scouting-report-result';

export type LocationSearchMode = 'keyword' | 'meaning';
export const SHOOT_TYPES: readonly ShootType[] = [
  'Portraits',
  'Family portraits',
  'Headshots',
  'Engagement',
  'Events',
];
export const TIMES_OF_DAY: readonly TimeOfDay[] = [
  'Dawn',
  'Morning',
  'Midday',
  'Afternoon',
  'Golden hour',
  'Blue hour',
  'Night',
];
export interface LocationSearchFilters {
  shootTypes: ShootType[];
  people: number | null;
  timesOfDay: TimeOfDay[];
  setting: LocationSetting | null;
  tags: string[];
}
export interface LocationSearchRequest extends LocationSearchFilters {
  query: string;
  mode: LocationSearchMode;
  cursor?: string;
}
