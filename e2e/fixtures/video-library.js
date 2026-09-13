const THUMBNAIL =
  'data:image/svg+xml;utf8,' +
  encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="160" height="90"><rect width="160" height="90" fill="#888"/></svg>');
const TOPICS = ['posing', 'lighting', 'interview'];

export class VideoLibrary {
  constructor(count = 7) {
    this.items = Array.from({ length: count }, (_, index) => {
      const videoId = `video${String(index + 1).padStart(6, '0')}`;
      return {
        id: `video-${index + 1}`,
        title: `Video ${String(index + 1).padStart(2, '0')}`,
        url: `https://www.youtube.com/watch?v=${videoId}`,
        videoId,
        thumbnailUrl: THUMBNAIL,
        topic: TOPICS[index % 3],
        channel: index % 2 ? null : 'Studio Notes',
        summary: index % 3 === 1 ? 'One light, many looks.' : null,
        notes: index === 0 ? 'Rewatch the hands section.' : null,
        createdAt: `2026-09-${String(30 - (index % 28)).padStart(2, '0')}T12:00:00Z`,
        revision: 1,
        tags: [
          { name: 'portrait', category: 'genre', provenance: 'manual' },
          ...(index % 3 === 0 ? [{ name: 'hands', category: 'technique', provenance: 'manual' }] : []),
        ],
        indexed: index !== 1,
        score: null,
      };
    });
    this.calls = [];
    this.failures = 0;
    this.meaningError = null;
    this.nextId = count + 1;
  }
  async attach(page) {
    await page.exposeFunction('loupeVideos', async (operation, input) => {
      this.calls.push({ operation, ...input });
      if (this.failures > 0) {
        this.failures--;
        return { error: 'request_failed' };
      }
      if (operation === 'list') {
        if (input.mode === 'meaning') {
          if (!input.query?.trim()) return { error: 'invalid_request', errors: { query: ['Describe what you are looking for.'] } };
          if (this.meaningError) return { error: this.meaningError };
        }
        const query = (input.query || '').toLowerCase();
        let matches = this.items.filter(
          (item) =>
            (!input.topic || item.topic === input.topic) &&
            (input.tags ?? []).every((tag) => item.tags.some((existing) => existing.name.toUpperCase() === tag.toUpperCase())) &&
            (input.mode === 'meaning'
              ? item.indexed
              : [item.title, item.channel, item.summary, item.notes, ...item.tags.map((tag) => tag.name)].some((value) =>
                  value?.toLowerCase().includes(query),
                )),
        );
        if (input.mode === 'meaning') {
          const words = query.split(/\s+/).filter(Boolean);
          matches = matches
            .map((item) => ({
              ...item,
              score: [item.title, item.summary, item.channel, ...item.tags.map((tag) => tag.name)].some((value) =>
                words.some((word) => value?.toLowerCase().includes(word)),
              )
                ? 0.91
                : 0.34,
            }))
            .sort((a, b) => b.score - a.score || a.id.localeCompare(b.id));
          return { data: { items: matches.slice(0, 24), nextCursor: null, totalCount: matches.length } };
        }
        const offset = Number(input.cursor || 0);
        return { data: { items: matches.slice(offset, offset + 24), nextCursor: offset + 24 < matches.length ? String(offset + 24) : null, totalCount: matches.length } };
      }
      if (operation === 'tags') {
        const counts = new Map();
        for (const item of this.items) for (const tag of item.tags) counts.set(tag.name, (counts.get(tag.name) ?? 0) + 1);
        return { data: [...counts].map(([name, count]) => ({ name, count })).sort((a, b) => b.count - a.count || a.name.localeCompare(b.name)) };
      }
      if (operation === 'get') {
        const item = this.items.find((item) => item.id === input.id);
        return item ? { data: item } : { error: 'item_unavailable' };
      }
      if (operation === 'save') {
        const match = /(?:v=|youtu\.be\/|shorts\/)([\w-]{11})/.exec(input.url || '');
        if (!match) return { error: 'invalid_request', errors: { url: ['Use a YouTube video URL.'] } };
        if (this.items.some((item) => item.videoId === match[1])) return { error: 'video_conflict' };
        const item = {
          id: `video-${this.nextId++}`,
          ...input,
          videoId: match[1],
          url: `https://www.youtube.com/watch?v=${match[1]}`,
          thumbnailUrl: THUMBNAIL,
          createdAt: '2026-10-01T12:00:00Z',
          revision: 1,
          tags: input.tags.map((tag) => ({ ...tag, provenance: 'manual' })),
          indexed: false,
          score: null,
        };
        this.items.unshift(item);
        return { data: item };
      }
      if (operation === 'update') {
        const item = this.items.find((item) => item.id === input.id);
        if (!item) return { error: 'item_unavailable' };
        if (item.revision !== input.revision) return { error: 'revision_conflict' };
        const match = /(?:v=|youtu\.be\/|shorts\/)([\w-]{11})/.exec(input.url || '');
        if (!match) return { error: 'invalid_request', errors: { url: ['Use a YouTube video URL.'] } };
        if (this.items.some((other) => other.id !== item.id && other.videoId === match[1])) return { error: 'video_conflict' };
        Object.assign(item, input, {
          videoId: match[1],
          url: `https://www.youtube.com/watch?v=${match[1]}`,
          revision: item.revision + 1,
          tags: input.tags.map((tag) => ({ ...tag, provenance: 'manual' })),
          indexed: false,
        });
        return { data: item };
      }
      if (operation === 'delete') {
        const item = this.items.find((item) => item.id === input.id);
        if (!item) return { error: 'item_unavailable' };
        if (item.revision !== input.revision) return { error: 'revision_conflict' };
        this.items = this.items.filter((other) => other.id !== input.id);
        return { data: null };
      }
      throw new Error('Unexpected video operation: ' + operation);
    });
  }
}
