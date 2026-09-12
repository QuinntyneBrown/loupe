export class ReferenceAnalysis {
  constructor(library) { this.library = library; this.saved = new Map(); this.operations = new Map(); this.calls = []; this.errors = []; this.undo = new Map(); }
  seed(id) {
    const result = { operationId: crypto.randomUUID(), imageRevision: 1, createdAt: '2026-09-03T12:00:00Z', mode: 'Live', model: 'fixture', promptVersion: 'reference-analysis-v1', description: 'Warm window light across a quiet room.', descriptionState: 'pending', tags: [{ name: 'soft light', category: 'lighting', state: 'pending' }, { name: 'quiet', category: 'mood', state: 'pending' }] };
    this.saved.set(id, result); return result;
  }
  async attach(page) {
    await page.exposeFunction('loupeReferenceAnalysis', async (operation, input) => {
      this.calls.push({ operation, ...input });
      if (['review', 'undo', 'request'].includes(operation) && this.errors.length) return { error: this.errors.shift() };
      const item = this.library.items.find(item => item.id === input.id);
      if (!item) return { error: 'item_unavailable' };
      if (operation === 'current') return { data: this.operations.get(input.id) || null };
      if (operation === 'suggestions') return { data: this.saved.get(input.id) || null };
      if (item.revision !== input.revision) return { error: 'revision_conflict' };
      if (operation === 'request') {
        const result = { id: crypto.randomUUID(), resourceId: item.id, type: 'ReferenceAnalysis', status: 'Queued', mode: 'Live', message: 'Waiting to start.' };
        this.operations.set(item.id, result); return { data: result };
      }
      if (operation === 'undo') {
        const snapshot = this.undo.get(item.id); if (!snapshot || snapshot.revision !== input.revision) return { error: 'revision_conflict' };
        Object.assign(item, snapshot.item, { revision: item.revision + 1 }); this.saved.set(item.id, snapshot.suggestions); this.undo.delete(item.id); return { data: item };
      }
      if (operation === 'review') {
        const suggestions = this.saved.get(item.id);
        if (!suggestions || suggestions.operationId !== input.operationId) return { error: 'revision_conflict' };
        const next = structuredClone(item), saved = structuredClone(suggestions);
        const accept = input.decision === 'accept', state = accept ? 'accepted' : 'dismissed';
        if (input.target === 'all' || input.target === 'description') {
          if (saved.descriptionState === 'pending') {
            if (accept) { next.description = (input.value ?? saved.description).trim(); next.descriptionProvenance = next.description === saved.description ? 'ai-accepted' : 'edited-ai'; }
            saved.descriptionState = state;
          }
        }
        for (const tag of saved.tags.filter(tag => tag.state === 'pending' && (input.target === 'all' || input.target === 'tag' && input.name === tag.name))) {
          if (accept) {
            const name = (input.target === 'tag' ? input.value ?? tag.name : tag.name).trim().normalize('NFC'), category = input.category ?? tag.category;
            if (!next.tags.some(active => active.name.toUpperCase() === name.toUpperCase())) {
              if (next.tags.length >= 50) return { error: 'invalid_request' };
              next.tags.push({ name, category, provenance: name === tag.name && category === tag.category ? 'ai-accepted' : 'edited-ai' });
            }
          }
          tag.state = state;
        }
        this.undo.set(item.id, { revision: item.revision + 1, item: structuredClone(item), suggestions: structuredClone(suggestions) });
        Object.assign(item, next, { revision: item.revision + 1 }); this.saved.set(item.id, saved); return { data: item };
      }
      throw new Error('Unexpected reference analysis operation: ' + operation);
    });
  }
}
