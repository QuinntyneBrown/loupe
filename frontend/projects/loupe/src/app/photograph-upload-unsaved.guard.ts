import { CanDeactivateFn } from '@angular/router';
import { MyWork } from './pages/my-work/my-work';

export const photographUploadUnsavedGuard: CanDeactivateFn<MyWork> = (page) => page.canLeave();
