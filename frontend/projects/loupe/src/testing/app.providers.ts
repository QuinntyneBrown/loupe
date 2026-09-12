import { REFERENCE_IMPORT_SERVICE } from 'api';
import { SEARCH_SERVICE } from 'api';
import { MockSearchService } from './mock-search.service';
import { PHOTOGRAPHER_SUMMARY_SERVICE } from 'api';
import { MockPhotographerSummaryService } from './mock-photographer-summary.service';
import { REFERENCE_ANALYSIS_SERVICE } from 'api';
import { MockReferenceAnalysisService } from './mock-reference-analysis.service';
import { MockReferenceImportService } from './mock-reference-import.service';
import { REFERENCE_SERVICE } from 'api';
import { MockReferenceService } from './mock-reference.service';
import { SESSION_SERVICE, PHOTOGRAPH_SERVICE, DELETION_SERVICE, CRITIQUE_SERVICE } from 'api';
import { MockCritiqueService } from './mock-critique.service';
import { MockDeletionService } from './mock-deletion.service';
import { MockSessionService } from './mock-session.service';
import { MockPhotographService } from './mock-photograph.service';
import { COMPARISON_SERVICE } from 'api';
import { MockComparisonService } from './mock-comparison.service';
import { BOARD_SERVICE } from 'api';
import { PHOTOGRAPHER_SERVICE } from 'api';
import { PHOTOGRAPHER_DRAFT_SERVICE } from 'api';
import { MockPhotographerDraftService } from './mock-photographer-draft.service';
import { MockPhotographerService } from './mock-photographer.service';
import { MockBoardService } from './mock-board.service';
import { REFERENCE_DRAFT_SERVICE } from 'api';
import { MockReferenceDraftService } from './mock-reference-draft.service';

export const appProviders = [
  { provide: SEARCH_SERVICE, useClass: MockSearchService },
  { provide: PHOTOGRAPHER_SUMMARY_SERVICE, useClass: MockPhotographerSummaryService },
  { provide: PHOTOGRAPHER_DRAFT_SERVICE, useClass: MockPhotographerDraftService },
  { provide: PHOTOGRAPHER_SERVICE, useClass: MockPhotographerService },
  { provide: REFERENCE_ANALYSIS_SERVICE, useClass: MockReferenceAnalysisService },
  { provide: REFERENCE_DRAFT_SERVICE, useClass: MockReferenceDraftService },
  { provide: BOARD_SERVICE, useClass: MockBoardService },
  { provide: REFERENCE_IMPORT_SERVICE, useClass: MockReferenceImportService },
  { provide: REFERENCE_SERVICE, useClass: MockReferenceService },
  { provide: COMPARISON_SERVICE, useClass: MockComparisonService },
  { provide: SESSION_SERVICE, useClass: MockSessionService },
  { provide: PHOTOGRAPH_SERVICE, useClass: MockPhotographService },
  { provide: DELETION_SERVICE, useClass: MockDeletionService },
  { provide: CRITIQUE_SERVICE, useClass: MockCritiqueService },
];
