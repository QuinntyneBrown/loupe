import { PhotographerDrafts } from './photographer-drafts.js';
export class PhotographerLibrary {
  constructor(count = 7) {
    this.items = Array.from({length: count}, (_, index) => ({
      id: `photographer-${index + 1}`, name: `Photographer ${String(index + 1).padStart(2, '0')}`,
      portfolioUrl: `https://portfolio${index + 1}.example/work`, createdAt: '2026-08-12T12:00:00Z',
      summary: 'Portraits in available light, mostly north-facing windows and long sittings. A restrained warm palette and almost no fill.',
      tags: [{name:'window light',category:'lighting',provenance:'manual'},{name:'portrait',category:'genre',provenance:'manual'}],
      referenceCount: index ? 0 : 6, references: [],
    }));
    this.calls = []; this.failures = 0; this.drafts=new PhotographerDrafts(this);
  }
  async attach(page) {
    await this.drafts.attach(page);
    await page.exposeFunction('loupePhotographers', async (operation, input) => {
      this.calls.push({operation,...input});
      if (this.failures > 0) {this.failures--; return {error:'request_failed'};}
      if (operation === 'list') {
        const offset = Number(input.cursor || 0);
        return {data:{items:this.items.slice(offset,offset+24),nextCursor:offset+24<this.items.length?String(offset+24):null,totalCount:this.items.length}};
      }
      throw new Error('Unexpected photographer operation: '+operation);
    });
  }
}
