import { CanDeactivateFn } from '@angular/router';
import type { VideosPage } from './pages/videos/videos-page';

export const videoUnsavedGuard: CanDeactivateFn<VideosPage> = (page) => page.canLeave();
