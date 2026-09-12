import { REFERENCE_IMPORT_SERVICE, ReferenceImportService } from 'api';
import {
  PHOTOGRAPHER_SUMMARY_SERVICE,
  PhotographerSummaryService,
  SEARCH_SERVICE,
  SearchService,
} from 'api';
import { REFERENCE_ANALYSIS_SERVICE, ReferenceAnalysisService } from 'api';
import { REFERENCE_SERVICE, ReferenceService } from 'api';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { SESSION_SERVICE, SessionService, PHOTOGRAPH_SERVICE, PhotographService } from 'api';
import { DELETION_SERVICE, DeletionService } from 'api';
import { CRITIQUE_SERVICE, CritiqueService } from 'api';
import { COMPARISON_SERVICE, ComparisonService } from 'api';
import { BOARD_SERVICE, BoardService } from 'api';
import { PHOTOGRAPHER_SERVICE, PhotographerService } from 'api';
import { PHOTOGRAPHER_DRAFT_SERVICE, PhotographerDraftService } from 'api';
import { REFERENCE_DRAFT_SERVICE, ReferenceDraftService } from 'api';

export const appProviders = [
  { provide: SEARCH_SERVICE, useClass: SearchService },
  { provide: PHOTOGRAPHER_SUMMARY_SERVICE, useClass: PhotographerSummaryService },
  { provide: PHOTOGRAPHER_DRAFT_SERVICE, useClass: PhotographerDraftService },
  { provide: PHOTOGRAPHER_SERVICE, useClass: PhotographerService },
  { provide: REFERENCE_ANALYSIS_SERVICE, useClass: ReferenceAnalysisService },
  { provide: REFERENCE_DRAFT_SERVICE, useClass: ReferenceDraftService },
  { provide: BOARD_SERVICE, useClass: BoardService },
  { provide: REFERENCE_IMPORT_SERVICE, useClass: ReferenceImportService },
  { provide: REFERENCE_SERVICE, useClass: ReferenceService },
  { provide: COMPARISON_SERVICE, useClass: ComparisonService },
  provideHttpClient(withXhr()),
  { provide: SESSION_SERVICE, useClass: SessionService },
  { provide: PHOTOGRAPH_SERVICE, useClass: PhotographService },
  { provide: DELETION_SERVICE, useClass: DeletionService },
  { provide: CRITIQUE_SERVICE, useClass: CritiqueService },
];
