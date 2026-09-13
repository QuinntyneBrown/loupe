import { CanDeactivateFn } from '@angular/router';
import type { VideoDetailPage } from './pages/video-detail/video-detail-page';

export const videoDetailUnsavedGuard: CanDeactivateFn<VideoDetailPage> = (page) => page.canLeave();
