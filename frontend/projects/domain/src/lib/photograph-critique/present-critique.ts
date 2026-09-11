import { SavedCritique, EvidenceStatement } from 'api';
import { CritiquePresentation, CritiqueEvidence } from 'components';

const briefFields = [
  { key: 'intent', label: 'Intent' },
  { key: 'genre', label: 'Genre' },
  { key: 'experience', label: 'Experience' },
  { key: 'requestedFeedback', label: 'Requested feedback' },
] as const;
const evidenceLabels: Record<string, string> = {
  VisibleObservation: 'Visible observation',
  ExifFact: 'EXIF fact',
  Hypothesis: 'Hypothesis',
  StylisticPreference: 'Stylistic preference',
};
const exifLabels: Record<string, string> = {
  Camera: 'Camera',
  Lens: 'Lens',
  Aperture: 'Aperture',
  ShutterSpeed: 'Shutter speed',
  Iso: 'ISO',
  FocalLength: 'Focal length',
  CapturedAt: 'Capture time',
};
const aspectGroups = [
  {
    label: 'Technical',
    fields: [
      { key: 'exposure', label: 'Exposure' },
      { key: 'focus', label: 'Focus' },
      { key: 'depthOfField', label: 'Depth of field' },
      { key: 'motion', label: 'Motion' },
      { key: 'lighting', label: 'Lighting' },
      { key: 'color', label: 'Color' },
      { key: 'processing', label: 'Processing' },
    ],
  },
  {
    label: 'Composition and story',
    fields: [
      { key: 'framing', label: 'Framing' },
      { key: 'subjectSeparation', label: 'Subject separation' },
      { key: 'balance', label: 'Balance' },
      { key: 'visualHierarchy', label: 'Visual hierarchy' },
      { key: 'mood', label: 'Mood' },
    ],
  },
] as const;

function presentEvidence(statements: EvidenceStatement[]): CritiqueEvidence[] {
  return statements.map((item) => ({
    label: evidenceLabels[item.kind],
    region: item.kind === 'VisibleObservation' ? (item.region ?? null) : null,
    statement: item.statement,
    fact: item.exifField ? `${exifLabels[item.exifField]}: ${item.exifValue}` : null,
  }));
}

export function presentCritique(item: SavedCritique): CritiquePresentation {
  return {
    generatedAt: item.generatedAt,
    model: item.model,
    promptVersion: item.promptVersion,
    strengths: item.content.strengths.map((value) => ({
      ...value,
      evidence: presentEvidence(value.evidence),
    })),
    priorities: item.content.improvements.map((value) => ({
      ...value,
      evidence: presentEvidence(value.evidence),
    })),
    exercise: item.content.exercise,
    groups: aspectGroups.map((group) => ({
      label: group.label,
      observations: group.fields.map((field) => {
        const value = item.content[field.key];
        return {
          label: field.label,
          explanation: value.explanation,
          uncertainty: value.assessable ? null : value.uncertaintyReason,
          evidence: presentEvidence(value.evidence),
        };
      }),
    })),
    brief: briefFields.flatMap((field) =>
      item.brief[field.key] ? [{ label: field.label, value: item.brief[field.key]! }] : [],
    ),
  };
}
