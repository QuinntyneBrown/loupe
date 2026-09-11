export class PhotographerSummaries {
  constructor(library) { this.library=library;this.operation=null;this.value=null;this.calls=[];this.failures={};this.receipts=new Map(); }
  queued() {this.operation={id:'summary-operation',resourceId:this.library.items[0].id,type:'PhotographerSummary',status:'Running',mode:'Live',createdAt:'2026-09-11T12:00:00Z',updatedAt:'2026-09-11T12:00:00Z',completedAt:null,nextAttemptAt:null,retryAvailableAt:null,failureCode:null,message:'Reading the site…'};}
  complete() {if(!this.operation)this.queued();this.operation.status='Succeeded';this.value={operationId:this.operation.id,sourceRevision:this.library.items[0].sourceRevision,createdAt:'2026-09-11T12:01:00Z',mode:'Live',model:'fixture',promptVersion:'photographer-summary-v1',source:this.library.items[0].source,summary:'Window-light portraits described by the captured portfolio page.',summaryStatus:'pending',tags:[{name:'soft light',category:'lighting',state:'pending'}],unavailableReason:null};this.library.items[0].revision++;}
  fail(code) {this.queued();this.operation.status='Failed';this.operation.failureCode=code;}
  async attach(page) {await page.exposeFunction('loupePhotographerSummaries',async(operation,input)=>{
    this.calls.push(operation);if(this.failures[operation]>0){this.failures[operation]--;return {error:'request_failed'};}
    const item=this.library.items.find(item=>item.id===input.id);if(!item)return {error:'item_unavailable'};
    if(operation==='current')return {data:this.operation};if(operation==='suggestions')return {data:this.value};
    if(operation==='request'){if(this.receipts.has(input.operationKey))return {data:this.receipts.get(input.operationKey)};if(item.revision!==input.revision)return {error:'revision_conflict'};this.queued();this.receipts.set(input.operationKey,this.operation);return {data:this.operation};}
    if(operation==='review'){
      if(item.revision!==input.revision||this.value?.operationId!==input.operationId||this.value.sourceRevision!==item.sourceRevision)return {error:'revision_conflict'};
      this.undo={revision:item.revision+1,summary:item.summary,summaryProvenance:item.summaryProvenance,tags:structuredClone(item.tags),value:structuredClone(this.value)};
      const accept=input.decision==='accept',state=accept?'accepted':'dismissed';
      if((input.target==='summary'||input.target==='all')&&this.value.summaryStatus==='pending'){if(accept){item.summary=input.value??this.value.summary;item.summaryProvenance=item.summary===this.value.summary?'ai-accepted':'edited-ai';}this.value.summaryStatus=state;}
      if(input.target==='tag'||input.target==='all')for(const tag of this.value.tags.filter(tag=>tag.state==='pending'&&(input.target==='all'||tag.name===input.name))){if(accept){const name=input.target==='tag'?(input.value??tag.name):tag.name,category=input.target==='tag'?(input.category??tag.category):tag.category;if(!item.tags.some(item=>item.name.toUpperCase()===name.toUpperCase()))item.tags.push({name,category,provenance:name===tag.name&&category===tag.category?'ai-accepted':'edited-ai'});}tag.state=state;}
      item.revision++;return {data:item};
    }
    if(operation==='undo'){if(item.revision!==input.revision||this.undo?.revision!==input.revision)return {error:'revision_conflict'};item.summary=this.undo.summary;item.summaryProvenance=this.undo.summaryProvenance;item.tags=this.undo.tags;this.value=this.undo.value;this.undo=null;item.revision++;return {data:item};}
    return {error:'item_unavailable'};
  });}
}
