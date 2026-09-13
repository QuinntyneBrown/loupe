import { InjectionToken } from '@angular/core';
import { VideoInput } from './video-input';
import { VideoPage, VideoResult, VideoTagFacet } from './video-result';
import { VideoSearchRequest } from './video-search-request';

export interface IVideoService {
  list(request?: VideoSearchRequest): Promise<VideoPage>;
  tags(): Promise<VideoTagFacet[]>;
  get(id: string): Promise<VideoResult>;
  save(input: VideoInput): Promise<VideoResult>;
  update(id: string, input: VideoInput & { revision: number }): Promise<VideoResult>;
  delete(id: string, revision: number): Promise<void>;
}
export const VIDEO_SERVICE = new InjectionToken<IVideoService>('VIDEO_SERVICE');
