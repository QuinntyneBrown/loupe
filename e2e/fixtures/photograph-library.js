import { expect } from '@playwright/test';

export class PhotographLibrary {
  constructor(count) {
    this.failures = { list: 0, get: 0, updateNotes: 0, updateBrief: 0 };
    this.gates = {};
    this.calls = [];
    this.errors = { upload: [] };
    this.uploadReceipts = new Map();
    this.deletions = new Map();
    this.lostUploadResponses = 0;
    const imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"><rect width="800" height="600" fill="#e0e0e0"/><path d="M0 600 450 0h100L100 600" fill="#9a9a9a"/></svg>');
    this.imageUrl = imageUrl;
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
    await page.exposeFunction('loupePhotographs', async (operation, input) => {
      this.calls.push(operation);
      await this.gates[operation]?.promise;
      const error = this.errors[operation]?.shift();
      if (error) return { error };
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
      if (operation === 'deletePhotograph') {
        const previous = [...this.deletions.values()].find(deletion => deletion.resourceId === input.id);
        if (previous) return { data: previous };
        const photo = this.photos.find(photo => photo.id === input.id);
        if (!photo) return { error: 'item_unavailable' };
        if (photo.revision !== input.revision) return { error: 'revision_conflict' };
        const deletion = { id: crypto.randomUUID(), resourceId: input.id, status: 'Pending', deletedAt: new Date().toISOString(), completedAt: null };
        this.deletions.set(deletion.id, deletion);
        this.photos = this.photos.filter(photo => photo.id !== input.id);
        return { data: deletion };
      }
      if (operation === 'getDeletion') {
        const deletion = this.deletions.get(input.id);
        return deletion ? { data: deletion } : { error: 'item_unavailable' };
      }
      if (operation === 'upload') {
        const clean = value => value?.replace(/\r\n?/g, '\n').trim() || null;
        const title = clean(input.title) || input.filename.replace(/\.[^.]*$/, '') || 'Untitled photograph';
        const brief = { intent: clean(input.brief.intent), genre: clean(input.brief.genre), experience: input.brief.experience, requestedFeedback: clean(input.brief.requestedFeedback) };
        const fingerprint = JSON.stringify({ title, brief, hash: input.hash, contentType: input.contentType });
        const receipt = this.uploadReceipts.get(input.operationKey);
        if (receipt) {
          if (receipt.fingerprint !== fingerprint) return { error: 'operation_conflict' };
          const photo = this.photos.find(photo => photo.id === receipt.id);
          return photo ? { data: photo } : { error: 'item_unavailable' };
        }
        const photo = {
          id: crypto.randomUUID(), title,
          createdAt: new Date().toISOString(), width: 800, height: 600,
          imageUrl: this.imageUrl, previewUrl: this.imageUrl, revision: 1, exif: {}, notes: null,
          brief,
        };
        this.photos.unshift(photo);
        this.uploadReceipts.set(input.operationKey, { id: photo.id, fingerprint });
        if (this.lostUploadResponses > 0) { this.lostUploadResponses--; return { error: 'request_failed' }; }
        return { data: photo };
      }
      if (operation === 'updateNotes') {
        const photo = this.photos.find(photo => photo.id === input.id);
        if (!photo) return { error: 'item_unavailable' };
        if (photo.revision !== input.revision) return { error: 'revision_conflict' };
        const notes = input.notes.replace(/\r\n?/g, '\n').trim();
        if ([...notes].length > 10000) return { error: 'invalid_request' };
        photo.notes = notes || null;
        photo.revision++;
        return { data: photo };
      }
      if (operation === 'updateBrief') {
        const photo = this.photos.find(photo => photo.id === input.id);
        if (!photo) return { error: 'item_unavailable' };
        if (photo.revision !== input.revision) return { error: 'revision_conflict' };
        const clean = value => value?.replace(/\r\n?/g, '\n').trim() || null;
        const brief = { intent: clean(input.intent), genre: clean(input.genre), experience: clean(input.experience), requestedFeedback: clean(input.requestedFeedback) };
        if ([...brief.intent ?? ''].length > 2000 || [...brief.genre ?? ''].length > 100 || [...brief.requestedFeedback ?? ''].length > 2000)
          return { error: 'invalid_request' };
        photo.brief = brief;
        photo.revision++;
        return { data: photo };
      }
      throw new Error(`Unknown photograph fixture operation: ${operation}`);
    });
  }
  pause(operation) {
    const gate = {};
    gate.promise = new Promise(resolve => { gate.release = resolve; });
    this.gates[operation] = gate;
  }
  release(operation) { this.gates[operation].release(); delete this.gates[operation]; }
  async reportUploadProgress(page, progress) {
    await page.evaluate(detail => window.dispatchEvent(new CustomEvent('loupe-upload-progress', { detail })), progress);
  }
  expectReadsOnly() { expect(this.calls.every(operation => ['list', 'get'].includes(operation))).toBe(true); }
  expectSavedNotes(value) { expect(this.photos[0].notes).toBe(value); }
  completeDeletion() {
    const deletion = [...this.deletions.values()][0];
    deletion.status = 'Completed';
    deletion.completedAt = new Date().toISOString();
  }
}
