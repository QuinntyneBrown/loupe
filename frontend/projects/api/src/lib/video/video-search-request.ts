import { VideoTopic } from './video-topic';

export interface VideoSearchRequest {
  query?: string;
  topic?: VideoTopic | null;
  tags?: string[];
  mode?: 'keyword' | 'meaning';
  cursor?: string;
}
