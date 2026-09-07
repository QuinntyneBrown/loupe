import { Injectable } from '@angular/core';
import { IPhotographService, PhotographPage } from 'api';

@Injectable()
export class MockPhotographService implements IPhotographService {
  async list(cursor?: string): Promise<PhotographPage> {
    const fixture = (
      window as Window & { loupeFixture?: { photoCount?: number; photoListFailures?: number } }
    ).loupeFixture;
    if (fixture?.photoListFailures) {
      fixture.photoListFailures--;
      throw new Error('Controlled collection failure');
    }
    const start = Number(cursor ?? 0);
    const count = fixture?.photoCount ?? 0;
    const end = Math.min(count, start + 24);
    const previewUrl =
      'data:image/svg+xml,' +
      encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"><rect width="800" height="600" fill="#e0e0e0"/><path d="M0 600 450 0h100L100 600" fill="#9a9a9a"/></svg>',
      );
    return {
      items: Array.from({ length: end - start }, (_, offset) => ({
        id: String(start + offset + 1),
        title: `Study ${String(start + offset + 1).padStart(2, '0')}`,
        createdAt: '2026-09-07T12:00:00Z',
        width: 800,
        height: 600,
        previewUrl,
      })),
      nextCursor: end < count ? String(end) : null,
    };
  }
}
