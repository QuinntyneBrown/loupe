export class PhotographerDrafts {
  constructor(library) {this.library=library; this.items=new Map(); this.receipts=new Map(); this.calls=[]; this.failure=null; this.hold=false; this.lostSaveResponses=0;}
  async attach(page) {
    await page.exposeFunction('loupePhotographerDrafts',async (operation,input)=>{
      this.calls.push({operation,...input});
      if (operation==='import') {
        if (this.receipts.has(input.operationKey)) return {data:this.receipts.get(input.operationKey)};
        const existing=this.library.items.find(item=>this.normalize(item.portfolioUrl)===this.normalize(input.portfolioUrl));
        const draft={id:crypto.randomUUID(),portfolioUrl:input.portfolioUrl,name:existing?.name||input.name||null,description:null,tags:[],revision:1,expiresAt:'2099-01-01T00:00:00Z',committedPhotographerId:existing?.id||null,failureCode:null,source:null,import:existing?null:{id:crypto.randomUUID(),status:'Queued',message:'Waiting to start.'}};
        this.items.set(draft.id,draft); this.receipts.set(input.operationKey,draft); return {data:draft};
      }
      if (operation==='save' && this.receipts.has(input.operationKey)) return {data:this.receipts.get(input.operationKey)};
      const draft=this.items.get(input.id);
      if (!draft) return {error:'item_unavailable'};
      if (operation==='cancel') {draft.canceled=true; return {data:null};}
      if (draft.canceled) return {error:'item_unavailable'};
      if (operation==='get') {
        if (draft.import?.status==='Queued' && !this.hold) {
          draft.import.status=this.failure?'Failed':'Succeeded'; draft.failureCode=this.failure; draft.revision++;
          if (!this.failure) {draft.name||='Casey Example'; draft.description='Interiors and portraits in available light.'; draft.tags=['interiors','film'];}
        }
        return {data:draft};
      }
      if (operation==='save') {
        if (input.revision!==draft.revision) return {error:'revision_conflict'};
        const existing=this.library.items.find(item=>this.normalize(item.portfolioUrl)===this.normalize(input.portfolioUrl));
        if (existing) return {data:{photographer:existing,alreadySaved:true}};
        const photographer={...input,id:crypto.randomUUID(),createdAt:'2026-09-11T12:00:00Z',name:input.name.trim(),summary:input.summary||null,notes:input.notes||null,tags:input.tags.map(tag=>({...tag,provenance:'manual'})),revision:1,referenceCount:0,references:[]};
        this.library.items.unshift(photographer); draft.committedPhotographerId=photographer.id;
        const result={photographer,alreadySaved:false}; this.receipts.set(input.operationKey,result);
        if (this.lostSaveResponses>0) {this.lostSaveResponses--; return {error:'request_failed'};}
        return {data:result};
      }
      throw new Error('Unexpected photographer draft operation: '+operation);
    });
  }
  normalize(value) {const url=new URL(value); url.hash='';return url.href;}
}
