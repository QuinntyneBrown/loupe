import { VideoTopic } from './video-topic';

export interface VideoTag {
  name: string;
  category: string | null;
  provenance: string;
}
export interface VideoResult {
  id: string;
  title: string;
  url: string;
  videoId: string;
  thumbnailUrl: string;
  topic: VideoTopic;
  channel: string | null;
  summary: string | null;
  notes: string | null;
  createdAt: string;
  revision: number;
  tags: VideoTag[];
  indexed: boolean;
  score: number | null;
}
export interface VideoPage {
  items: VideoResult[];
  nextCursor: string | null;
  totalCount: number;
}
export interface VideoTagFacet {
  name: string;
  count: number;
}
