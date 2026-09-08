import { CritiqueBrief } from '../photograph/critique-brief';

export interface EvidenceStatement {
  kind: 'VisibleObservation' | 'ExifFact' | 'Hypothesis' | 'StylisticPreference';
  statement: string;
  exifField: string | null;
  exifValue: string | null;
}
export interface CritiqueObservation {
  assessable: boolean;
  explanation: string;
  uncertaintyReason: string | null;
  evidence: EvidenceStatement[];
}
export interface CritiqueResult {
  strengths: { explanation: string; evidence: EvidenceStatement[] }[];
  exposure: CritiqueObservation;
  focus: CritiqueObservation;
  depthOfField: CritiqueObservation;
  motion: CritiqueObservation;
  lighting: CritiqueObservation;
  color: CritiqueObservation;
  processing: CritiqueObservation;
  framing: CritiqueObservation;
  subjectSeparation: CritiqueObservation;
  balance: CritiqueObservation;
  visualHierarchy: CritiqueObservation;
  mood: CritiqueObservation;
  improvements: {
    observation: string;
    effect: string;
    action: string;
    evidence: EvidenceStatement[];
  }[];
  exercise: { action: string; comparison: string };
}
export interface SavedCritique {
  operationId: string;
  generatedAt: string;
  mode: 'Demo' | 'Live';
  model: string;
  promptVersion: string;
  brief: CritiqueBrief;
  content: CritiqueResult;
}
