import { CanDeactivateFn } from '@angular/router';
import { ReferenceUploadPage } from './pages/reference-upload/reference-upload-page';
export const referenceUploadUnsavedGuard: CanDeactivateFn<ReferenceUploadPage> = (page) =>
  page.canLeave();
