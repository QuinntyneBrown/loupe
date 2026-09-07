export interface CritiqueBrief {
  intent: string | null;
  genre: string | null;
  experience: 'Beginner' | 'Intermediate' | 'Advanced' | 'Professional' | null;
  requestedFeedback: string | null;
}
