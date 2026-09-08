export class ReferenceLibrary {
  constructor(count) {
    const imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"><rect width="800" height="600" fill="#ededed"/><path d="M0 600 600 0h100L100 600" fill="#999"/></svg>');
    this.items = Array.from({ length: count }, (_, index) => ({
      id: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      title: `Reference ${String(index + 1).padStart(2, '0')}`, createdAt: '2026-09-08T12:00:00Z',
      width: 800, height: 600, imageUrl, previewUrl: imageUrl, revision: 1,
      sourceUrl: 'https://source.example/photo', attribution: 'Supplied photographer', notes: 'Study the separation.\nKeep the source context.',
    }));
    this.updates = []; this.lostUpdateResponses = 0;
    this.uploadReceipts = new Map(); this.lostUploadResponses = 0;
    this.calls = []; this.failures = {}; this.errors = {}; this.gates = {};
  }
  async attach(page) {
    await page.exposeFunction('loupeReferences', async (operation, input) => {
      this.calls.push(operation); await this.gates[operation]?.promise;
      const error = this.errors[operation]?.shift(); if (error) return { error };
      if (this.failures[operation] > 0) { this.failures[operation]--; return { error: 'request_failed' }; }
      if (operation === 'update') {
        this.updates.push(input);
        const item=this.items.find(item=>item.id===input.id);
        if(!item) return {error:'item_unavailable'};
        if(item.revision!==input.revision) return {error:'revision_conflict'};
        const normalize=value=>value?.replace(/\r\n?/g,'\n').trim()||null;
        for(const field of ['title','sourceUrl','attribution','notes']) item[field]=normalize(input[field]);
        item.revision++;
        if(this.lostUpdateResponses>0) {this.lostUpdateResponses--;return {error:'request_failed'};}
        return {data:item};
      }
      if (operation === 'upload') {
        const normalize=value => value?.replace(/\r\n?/g,'\n').trim() || null;
        const metadata={title:normalize(input.title)||input.filename.replace(/\.[^.]+$/,'')||'Untitled reference',sourceUrl:normalize(input.sourceUrl),attribution:normalize(input.attribution),notes:normalize(input.notes)};
        const fingerprint=JSON.stringify({...metadata,hash:input.hash,contentType:input.contentType});
        const receipt=this.uploadReceipts.get(input.operationKey);
        if(receipt && receipt.fingerprint!==fingerprint) return {error:'operation_conflict'};
        if(receipt) return {data:this.items.find(item=>item.id===receipt.id)};
        const item={...new ReferenceLibrary(1).items[0],...metadata,id:crypto.randomUUID()};
        this.items.unshift(item); this.uploadReceipts.set(input.operationKey,{fingerprint,id:item.id});
        if(this.lostUploadResponses>0) { this.lostUploadResponses--; return {error:'request_failed'}; }
        return {data:item};
      }
      if (operation === 'list') {
        const start = Number(input.cursor ?? 0), end = Math.min(start + 24, this.items.length);
        return { data: { items: this.items.slice(start, end), nextCursor: end < this.items.length ? String(end) : null } };
      }
      if (operation === 'get') {
        const item = this.items.find(item => item.id === input.id);
        return item ? { data: item } : { error: 'item_unavailable' };
      }
      throw new Error(`Unexpected reference operation: ${operation}`);
    });
  }
  async reportUploadProgress(page,progress) { await page.evaluate(progress=>window.dispatchEvent(new CustomEvent('loupe-reference-upload-progress',{detail:progress})),progress); }
  pause(operation) { let release; const promise = new Promise(resolve => { release = resolve; }); this.gates[operation] = { promise, release }; }
  release(operation) { this.gates[operation]?.release(); delete this.gates[operation]; }
}
