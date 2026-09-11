export interface EvidenceRegion {
  x: number;
  y: number;
  size: number;
}

export interface CritiqueEvidence {
  region: EvidenceRegion | null;
  label: string;
  statement: string;
  fact: string | null;
}
export interface CritiquePresentation {
  generatedAt: string;
  demo: boolean;
  model: string;
  promptVersion: string;
  strengths: { explanation: string; evidence: CritiqueEvidence[] }[];
  priorities: {
    observation: string;
    effect: string;
    action: string;
    evidence: CritiqueEvidence[];
  }[];
  exercise: { action: string; comparison: string };
  groups: {
    label: string;
    observations: {
      label: string;
      explanation: string;
      uncertainty: string | null;
      evidence: CritiqueEvidence[];
    }[];
  }[];
  brief: { label: string; value: string }[];
}
