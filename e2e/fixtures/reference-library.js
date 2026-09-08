export class ReferenceLibrary {
  constructor(count) {
    const imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"><rect width="800" height="600" fill="#ededed"/><path d="M0 600 600 0h100L100 600" fill="#999"/></svg>');
    this.items = Array.from({ length: count }, (_, index) => ({
      id: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      title: `Reference ${String(index + 1).padStart(2, '0')}`, createdAt: '2026-09-08T12:00:00Z',
      width: 800, height: 600, imageUrl, previewUrl: imageUrl, revision: 1,
      sourceUrl: 'https://source.example/photo', attribution: 'Supplied photographer', notes: 'Study the separation.\nKeep the source context.',
    }));
    this.importCalls=[];this.importOperations=new Map();this.importReceipts=new Map();this.importFailures={};this.importErrors={};this.lostImportResponses=0;
    this.linkReceipts=new Map(); this.lostLinkResponses=0;
    this.updates = []; this.lostUpdateResponses = 0;
    this.uploadReceipts = new Map(); this.lostUploadResponses = 0;
    this.calls = []; this.failures = {}; this.errors = {}; this.gates = {};
  }
  async attach(page) {
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
