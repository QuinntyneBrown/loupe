import { VideoTag } from './video-result';
import { VideoTopic } from './video-topic';

export interface VideoInput {
  title: string;
  url: string;
  topic: VideoTopic;
  channel: string | null;
  summary: string | null;
  notes: string | null;
  tags: Pick<VideoTag, 'name' | 'category'>[];
}
