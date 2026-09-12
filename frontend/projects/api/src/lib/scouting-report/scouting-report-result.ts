export type ShootType = 'Portraits' | 'Family portraits' | 'Headshots' | 'Engagement' | 'Events';
export type SuitabilityRating = 'Well suited' | 'Workable' | 'Not recommended' | 'Cannot assess';
export type TimeOfDay =
  'Dawn' | 'Morning' | 'Midday' | 'Afternoon' | 'Golden hour' | 'Blue hour' | 'Night';
export type TimeOfDayRating = 'Recommended' | 'Avoid' | 'Unknown';
export type CompositionTechnique =
  | 'Rule of thirds'
  | 'Leading lines'
  | 'Fill the frame'
  | 'Colour theory'
  | 'Near-far'
  | 'Simplify the scene'
  | 'Natural framing'
  | 'Symmetry'
  | 'Negative space'
  | 'Layering'
  | 'Patterns and repetition'
  | 'Vantage point';
export type EvidenceBasis = 'Visible' | 'Inferred';

export interface ReportEntry {
  basis: EvidenceBasis;
  citedImageIds: string[];
}
export interface ReportStrength extends ReportEntry {
  strength: string;
  reason: string;
}
export interface SuitabilityEntry extends ReportEntry {
  shootType: ShootType;
  rating: SuitabilityRating;
  reason: string;
}
export interface TimeOfDayEntry extends ReportEntry {
  period: TimeOfDay;
  rating: TimeOfDayRating;
  reason: string;
}
export interface TechniqueEntry extends ReportEntry {
  technique: CompositionTechnique;
  explanation: string;
}
export interface GroupSizeEntry extends ReportEntry {
  cannotAssess: boolean;
  minimum: number | null;
  maximum: number | null;
  reason: string;
}
export interface CautionEntry extends ReportEntry {
  caution: string;
}
export interface ScoutingReport {
  overview: ReportStrength[];
  suitability: SuitabilityEntry[];
  timesOfDay: TimeOfDayEntry[];
  techniques: TechniqueEntry[];
  groupSize: GroupSizeEntry;
  cautions: CautionEntry[];
}
export interface SavedScoutingReport {
  operationId: string;
  generatedAt: string;
  mode: 'Demo' | 'Live';
  model: string;
  promptVersion: string;
  briefSnapshot: string | null;
  imageSetRevision: number;
  imageCount: number;
  report: ScoutingReport;
}
