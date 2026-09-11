export class ReferenceLibrary {
  constructor(count) {
    const imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"><rect width="800" height="600" fill="#ededed"/><path d="M0 600 600 0h100L100 600" fill="#999"/></svg>');
    this.items = Array.from({ length: count }, (_, index) => ({
      id: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      title: `Reference ${String(index + 1).padStart(2, '0')}`, createdAt: '2026-09-08T12:00:00Z',
      width: 800, height: 600, imageUrl, previewUrl: imageUrl, revision: 1, boardIds: [], tags: [],
      sourceUrl: 'https://source.example/photo', attribution: 'Supplied photographer', notes: 'Study the separation.\nKeep the source context.',
    }));
    this.importCalls=[];this.importOperations=new Map();this.importReceipts=new Map();this.importFailures={};this.importErrors={};this.lostImportResponses=0;
    this.linkReceipts=new Map(); this.lostLinkResponses=0;
    this.updates = []; this.lostUpdateResponses = 0;
    this.uploadReceipts = new Map(); this.lostUploadResponses = 0;
    this.calls = []; this.failures = {}; this.errors = {}; this.gates = {};
    this.boards = []; this.boardCalls = [];
    this.drafts = new Map(); this.draftCalls = []; this.draftReceipts = new Map(); this.draftFailure = null; this.lostDraftResponses = 0;
  }
  async attach(page) {
    await page.exposeFunction('loupeReferenceDrafts', async (operation, input) => {
      this.draftCalls.push({ operation, ...input });
      await this.gates['draft-' + operation]?.promise;
      if (this.failures['draft-' + operation] > 0) { this.failures['draft-' + operation]--; return { error: 'request_failed' }; }
      if (input.operationKey && this.draftReceipts.has(input.operationKey)) return { data: this.draftReceipts.get(input.operationKey) };
      if (operation === 'import') {
        const existing = this.items.find(item => item.sourceUrl === input.sourceUrl);
        const draft = { ...new ReferenceLibrary(1).items[0], id: crypto.randomUUID(), title: existing?.title || 'source.example', attribution: null, imageUrl: null, previewUrl: null, sourceUrl: input.sourceUrl, committedReferenceId: existing?.id || null, failureCode: null, expiresAt: '2099-01-01T00:00:00Z', import: existing ? null : { id: crypto.randomUUID(), status: 'Queued', message: 'Waiting to start.' } };
        this.drafts.set(draft.id, draft); this.draftReceipts.set(input.operationKey, draft); return { data: draft };
      }
      if (operation === 'upload') {
        const base = new ReferenceLibrary(1).items[0];
        const draft = { ...base, id: crypto.randomUUID(), title: input.filename.replace(/\.[^.]+$/, ''), attribution: null, sourceUrl: input.sourceUrl || null, import: null, committedReferenceId: null, failureCode: null, expiresAt: '2099-01-01T00:00:00Z' };
        this.drafts.set(draft.id, draft); this.draftReceipts.set(input.operationKey, draft); return { data: draft };
      }
      const draft = this.drafts.get(input.id);
      if (operation === 'cancel') { this.drafts.delete(input.id); return { data: null }; }
      if (!draft) return { error: 'item_unavailable' };
      if (operation === 'get') {
        if (draft.import?.status === 'Queued') {
          draft.import.status = this.draftFailure ? 'Failed' : 'Succeeded'; draft.failureCode = this.draftFailure;
          draft.title = 'Morning by the window'; draft.attribution = this.draftFailure ? null : 'Casey Example'; draft.revision++;
          if (!this.draftFailure) { draft.imageUrl = new ReferenceLibrary(1).items[0].imageUrl; draft.previewUrl = draft.imageUrl; }
        }
        return { data: draft };
      }
      if (operation === 'save') {
        if (draft.revision !== input.revision) return { error: 'revision_conflict' };
        const existing = input.sourceUrl ? this.items.find(item => item.sourceUrl === input.sourceUrl) : null;
        if (existing) { const result = { reference: existing, alreadySaved: true }; this.draftReceipts.set(input.operationKey, result); return { data: result }; }
        const reference = { ...draft, ...input, id: crypto.randomUUID(), title: input.title.trim(), attribution: input.attribution?.trim() || null, tags: [], boardIds: input.boardIds, revision: 1 };
        this.items.unshift(reference); draft.committedReferenceId = reference.id;
        const result = { reference, alreadySaved: false }; this.draftReceipts.set(input.operationKey, result);
        if (this.lostDraftResponses > 0) { this.lostDraftResponses--; return { error: 'request_failed' }; }
        return { data: result };
      }
      throw new Error('Unexpected draft operation: ' + operation);
    });
    await page.exposeFunction('loupeBoards', async (operation, input) => {
      this.boardCalls.push({ operation, ...input });
      await this.gates['boards-' + operation]?.promise;
      if (this.failures['boards-' + operation] > 0) { this.failures['boards-' + operation]--; return { error: 'request_failed' }; }
      const counted = board => ({ ...board, referenceCount: this.items.filter(item => item.boardIds?.includes(board.id)).length });
      if (operation === 'list') return { data: this.boards.map(counted).sort((a, b) => a.name.toLowerCase().localeCompare(b.name.toLowerCase())) };
      if (operation === 'setMemberships') {
        const item = this.items.find(item => item.id === input.id);
        if (!item || input.boardIds.some(id => !this.boards.some(board => board.id === id))) return { error: 'item_unavailable' };
        if (item.revision !== input.revision) return { error: 'revision_conflict' };
        item.boardIds = [...new Set(input.boardIds)]; item.revision++;
        return { data: item };
      }
      const board = this.boards.find(board => board.id === input.id);
      if (operation !== 'create' && !board) return { error: 'item_unavailable' };
      if (board && board.revision !== input.revision) return { error: 'revision_conflict' };
      if (operation === 'delete') {
        this.boards = this.boards.filter(board => board.id !== input.id);
        this.items.forEach(item => { item.boardIds = (item.boardIds ?? []).filter(id => id !== input.id); });
        return { data: null };
      }
      const name = input.name.trim().normalize('NFC');
      if (!name || [...name].length > 80) return { error: 'invalid_request' };
      if (this.boards.some(other => other.id !== input.id && other.name.toUpperCase() === name.toUpperCase())) return { error: 'board_name_conflict' };
      if (operation === 'create') {
        const created = { id: crypto.randomUUID(), name, revision: 1 }; this.boards.push(created);
        return { data: counted(created) };
      }
      if (operation === 'rename') { board.name = name; board.revision++; return { data: counted(board) }; }
      throw new Error('Unexpected board operation: ' + operation);
    });
    await page.exposeFunction('loupeReferenceImports',async(operation,input)=>{
      this.importCalls.push({operation,...input});await this.gates['import-'+operation]?.promise;
      const error=this.importErrors[operation]?.shift();if(error)return {error};
      if(this.importFailures[operation]>0){this.importFailures[operation]--;return {error:'request_failed'};}
      const reference=this.items.find(item=>item.id===input.id);if(!reference)return {error:'item_unavailable'};
      if(operation==='current')return {data:this.importOperations.get(input.id)||null};
      if(operation==='request') {
        const fingerprint=JSON.stringify({id:input.id,revision:input.revision}),receipt=this.importReceipts.get(input.operationKey);
        if(receipt&&receipt.fingerprint!==fingerprint)return {error:'operation_conflict'};
        if(receipt)return {data:receipt.operation};
        if(reference.revision!==input.revision)return {error:'revision_conflict'};
        const current=this.importOperations.get(input.id);
        const result=current&&['Queued','Running'].includes(current.status)?current:{id:crypto.randomUUID(),resourceId:input.id,type:'ReferenceImport',status:'Queued',mode:'Demo',createdAt:'2026-09-08T12:00:00Z',updatedAt:'2026-09-08T12:00:00Z',completedAt:null,nextAttemptAt:null,retryAvailableAt:null,failureCode:null,message:'Waiting to start.'};
        this.importOperations.set(input.id,result);this.importReceipts.set(input.operationKey,{fingerprint,operation:result});
        if(this.lostImportResponses>0){this.lostImportResponses--;return {error:'request_failed'};}
        return {data:result};
      }
      throw new Error('Unexpected import operation: '+operation);
    });
    await page.exposeFunction('loupeReferences', async (operation, input) => {
      this.calls.push(operation); await this.gates[operation]?.promise;
      const error = this.errors[operation]?.shift(); if (error) return { error };
      if (this.failures[operation] > 0) { this.failures[operation]--; return { error: 'request_failed' }; }
      if(operation==='saveLink') {
        const clean=value=>value?.replace(/\r\n?/g,'\n').trim()||null;
        const metadata={title:clean(input.title)||new URL(input.sourceUrl.trim()).hostname,sourceUrl:clean(input.sourceUrl),attribution:clean(input.attribution),notes:clean(input.notes)};
        const fingerprint=JSON.stringify(metadata),receipt=this.linkReceipts.get(input.operationKey);
        if(receipt&&receipt.fingerprint!==fingerprint) return {error:'operation_conflict'};
        const normalized=value=>{const url=new URL(value);url.hash='';return url.href;};
        let item=receipt?this.items.find(item=>item.id===receipt.id):this.items.find(item=>item.sourceUrl&&normalized(item.sourceUrl)===normalized(metadata.sourceUrl));
        const alreadySaved=!!item;
        if(!item) {item={...new ReferenceLibrary(1).items[0],...metadata,id:crypto.randomUUID(),imageUrl:null,previewUrl:null,width:null,height:null};this.items.unshift(item);}
        this.linkReceipts.set(input.operationKey,{fingerprint,id:item.id});
        if(this.lostLinkResponses>0) {this.lostLinkResponses--;return {error:'request_failed'};}
        return {data:{reference:item,alreadySaved}};
      }
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
      if (operation === 'setTags') {
        const item = this.items.find(item => item.id === input.id);
        if (!item) return { error: 'item_unavailable' };
        if (item.revision !== input.revision) return { error: 'revision_conflict' };
        item.tags = input.tags.map(tag => ({ ...tag, provenance: 'manual' })); item.revision++;
        return { data: item };
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
      if (operation === 'tags') {
        const items = input.boardId ? this.items.filter(item => item.boardIds.includes(input.boardId)) : this.items;
        const tags = new Map();
        for (const item of items) for (const tag of item.tags) {
          const key = tag.name.normalize('NFC').toUpperCase();
          const current = tags.get(key) ?? { name: tag.name, referenceCount: 0 };
          current.referenceCount++; tags.set(key, current);
        }
        return { data: [...tags.values()].sort((a,b) => b.referenceCount-a.referenceCount || a.name.localeCompare(b.name)) };
      }
      if (operation === 'list') {
        if (input.boardId && !this.boards.some(board => board.id === input.boardId)) return { error: 'item_unavailable' };
        const items = this.items.filter(item => (!input.boardId || item.boardIds?.includes(input.boardId)) && (input.tags ?? []).every(tag => item.tags.some(active => active.name.normalize('NFC').toUpperCase() === tag.normalize('NFC').toUpperCase())));
        const start = Number(input.cursor ?? 0), end = Math.min(start + 24, items.length);
        return { data: { items: items.slice(start, end), nextCursor: end < items.length ? String(end) : null, totalCount: items.length, libraryCount: this.items.length } };
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
