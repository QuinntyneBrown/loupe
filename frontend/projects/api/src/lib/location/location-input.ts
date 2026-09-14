import { Coordinates, LocationSetting } from './location-result';

export interface LocationTagInput {
  name: string;
  category: string | null;
}
export interface LocationDetailsInput {
  name: string;
  addressLine1: string | null;
  addressLine2: string | null;
  locality: string | null;
  region: string | null;
  postalCode: string | null;
  country: string | null;
  coordinates: Coordinates | null;
  setting: LocationSetting | null;
}
export interface LocationInput extends LocationDetailsInput {
  scoutingBrief: string | null;
  notes: string | null;
  tags: LocationTagInput[];
}
