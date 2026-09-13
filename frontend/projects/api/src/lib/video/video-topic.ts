export const VIDEO_TOPICS = [
  'posing',
  'lighting',
  'interview',
  'composition',
  'editing',
  'gear',
  'other',
] as const;
export type VideoTopic = (typeof VIDEO_TOPICS)[number];
export const VIDEO_TOPIC_LABELS: Record<VideoTopic, string> = {
  posing: 'Posing',
  lighting: 'Lighting',
  interview: 'Interview',
  composition: 'Composition',
  editing: 'Editing',
  gear: 'Gear',
  other: 'Other',
};
