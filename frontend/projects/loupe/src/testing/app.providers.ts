import { REFERENCE_IMPORT_SERVICE } from 'api';
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
import { MockBoardService } from './mock-board.service';

export const appProviders = [
  { provide: BOARD_SERVICE, useClass: MockBoardService },
  { provide: REFERENCE_IMPORT_SERVICE, useClass: MockReferenceImportService },
  { provide: REFERENCE_SERVICE, useClass: MockReferenceService },
  { provide: COMPARISON_SERVICE, useClass: MockComparisonService },
  { provide: SESSION_SERVICE, useClass: MockSessionService },
  { provide: PHOTOGRAPH_SERVICE, useClass: MockPhotographService },
  { provide: DELETION_SERVICE, useClass: MockDeletionService },
  { provide: CRITIQUE_SERVICE, useClass: MockCritiqueService },
];
