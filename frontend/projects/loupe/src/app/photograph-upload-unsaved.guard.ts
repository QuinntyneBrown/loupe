import { CanDeactivateFn } from '@angular/router';
import { PhotographUploadPage } from './pages/photograph-upload/photograph-upload-page';

export const photographUploadUnsavedGuard: CanDeactivateFn<PhotographUploadPage> = (page) =>
  page.canLeave();
