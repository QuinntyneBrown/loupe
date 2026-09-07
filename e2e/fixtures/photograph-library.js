import { expect } from '@playwright/test';

export class PhotographLibrary {
  constructor(count) {
    this.failures = { list: 0, get: 0 };
    this.calls = [];
    const imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"><rect width="800" height="600" fill="#e0e0e0"/><path d="M0 600 450 0h100L100 600" fill="#9a9a9a"/></svg>');
    this.photos = Array.from({ length: count }, (_, index) => ({
      id: `00000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      title: `Study ${String(index + 1).padStart(2, '0')}`, createdAt: '2026-09-07T12:00:00Z',
      width: 800, height: 600, imageUrl, previewUrl: imageUrl, revision: 1,
      exif: { camera: 'Fixture camera', lens: '50mm lens', aperture: '2.8', shutterSpeed: '1/125', iso: '200', focalLength: '50mm', capturedAt: '2026:09:01 08:30:00' },
      brief: { intent: 'Explore quiet morning light', genre: 'Portrait', experience: 'Beginner', requestedFeedback: 'Improve the framing' },
      notes: 'Keep the edges quiet.\nTry a lower viewpoint.',
    }));
  }
  async attach(page) {
    await page.exposeFunction('loupePhotographs', (operation, input) => {
      this.calls.push(operation);
      if (this.failures[operation] > 0) {
        this.failures[operation]--;
        return { error: 'request_failed' };
      }
      if (operation === 'list') {
        const start = Number(input.cursor ?? 0);
        const end = Math.min(this.photos.length, start + 24);
        return { data: { items: this.photos.slice(start, end), nextCursor: end < this.photos.length ? String(end) : null } };
      }
      if (operation === 'get') {
        const photo = this.photos.find(photo => photo.id === input.id);
        return photo ? { data: photo } : { error: 'item_unavailable' };
      }
      throw new Error(`Unknown photograph fixture operation: ${operation}`);
    });
  }
  expectReadsOnly() { expect(this.calls.every(operation => ['list', 'get'].includes(operation))).toBe(true); }
}
