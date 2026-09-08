import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';

async function setup(page) {
  const work=new MyWorkPage(page);await work.configureCollection(0);
  const inspiration=new InspirationPage(page);await inspiration.configure(1);
  const signIn=new SignInPage(page);await signIn.openPrivateDestination();await signIn.continue();await inspiration.open();await inspiration.openReference('Reference 01');
  const detail=new ReferenceDetailPage(page);await detail.edit();
  return {work,inspiration,detail,signIn,item:inspiration.library.items[0]};
}
// Given a saved reference, when its metadata changes, then normalized values persist
// and a stale revision requires review while preserving the user's attempted draft.
test('L2-012.2: metadata edits persist after reload and preserve the saved image',async({page})=>{
  const {inspiration,detail,signIn,item}=await setup(page),image=item.imageUrl;
  await detail.fillMetadata({title:'  New title  ',sourceUrl:'https://source.example/new',attribution:' Supplied author ',notes:' New notes\nKeep the context. '});
  await detail.saveMetadata();await detail.expectMetadataClosed();await detail.expectSaved(item);
  expect(item.title).toBe('New title');expect(item.attribution).toBe('Supplied author');expect(item.notes).toBe('New notes\nKeep the context.');expect(item.imageUrl).toBe(image);expect(item.revision).toBe(2);
  await page.reload();await signIn.continue();await detail.expectSaved(item);expect(inspiration.library.updates).toHaveLength(1);
});
test('L2-012.2: clearing optional metadata shows honest absent values',async({page})=>{
  const {detail,item}=await setup(page);await detail.fillMetadata({sourceUrl:'',attribution:'',notes:''});await detail.saveMetadata();await detail.expectMetadataClosed();await detail.expectSaved(item);
  for(const field of ['sourceUrl','attribution','notes']) expect(item[field]).toBeNull();
});
for(const [field,maximum] of [['title',200],['sourceUrl',2048],['attribution',200],['notes',10000]]) test(`L2-012.2: reject overlong ${field} without saving`,async({page})=>{
  const {detail,inspiration}=await setup(page);await detail.fillMetadata({[field]:'x'.repeat(maximum+1)});
  await detail.expectMetadataFieldError(field,`Use ${maximum.toLocaleString('en-US')} characters or fewer.`);expect(inspiration.library.updates).toHaveLength(0);
});
test('L2-012.2: title remains required when editing',async({page})=>{
  const {detail}=await setup(page);await detail.fillMetadata({title:' '});await detail.expectMetadataFieldError('title','Enter a title.');
});
test('L2-012.2: invalid source URL cannot replace saved context',async({page})=>{
  const {detail}=await setup(page);await detail.fillMetadata({sourceUrl:'javascript:alert(1)'});await detail.expectMetadataFieldError('sourceUrl','Use an HTTP or HTTPS URL without credentials or a nonstandard port.');
});
test('L2-030: a failed metadata save retains its draft and retries',async({page})=>{
  const {detail,inspiration,item}=await setup(page);inspiration.library.failures.update=1;await detail.fillMetadata({notes:'My draft'});await detail.saveMetadata();await detail.expectMetadataError('The metadata save was not confirmed. Your changes are still here.');await detail.expectDraft({notes:'My draft'});
  await detail.saveMetadata();await detail.expectMetadataClosed();await detail.expectSaved(item);expect(item.notes).toBe('My draft');
});
test('L2-030: stale metadata keeps the draft and requires latest review before resubmission',async({page})=>{
  const {detail,inspiration,item}=await setup(page);item.notes='Other tab';item.revision++;
  await detail.fillMetadata({notes:'My draft'});await detail.saveMetadata();await detail.expectMetadataError('This reference changed. Your metadata draft has been kept.');await detail.expectMetadataDisabled();await detail.expectDraft({notes:'My draft'});
  await detail.reloadMetadata();await detail.expectLatestMetadata({notes:'Other tab'});await detail.expectDraft({notes:'My draft'});await detail.saveMetadata();await detail.expectMetadataClosed();await detail.expectSaved(item);
  expect(inspiration.library.updates.map(update=>update.revision)).toEqual([1,2]);expect(item.notes).toBe('My draft');
});
test('L2-030: failed conflict review preserves the draft and remains retryable',async({page})=>{
  const {detail,inspiration,item}=await setup(page);item.revision++;await detail.fillMetadata({notes:'Keep my draft'});await detail.saveMetadata();await detail.expectMetadataDisabled();
  inspiration.library.failures.get=1;await detail.reloadMetadata();await detail.expectMetadataError('Latest metadata could not be loaded. Your changes are still here.');await detail.expectMetadataDisabled();await detail.expectDraft({notes:'Keep my draft'});
  await detail.reloadMetadata();await detail.expectLatestMetadata({title:item.title});await detail.saveMetadata();await detail.expectMetadataClosed();
});
test('L2-030: lost metadata acknowledgment can be resolved by reviewing the saved values',async({page})=>{
  const {detail,inspiration,item}=await setup(page);inspiration.library.lostUpdateResponses=1;await detail.fillMetadata({notes:'Saved once'});await detail.saveMetadata();await detail.expectMetadataError('The metadata save was not confirmed. Your changes are still here.');
  await detail.saveMetadata();await detail.expectMetadataError('This reference changed. Your metadata draft has been kept.');await detail.reloadMetadata();await detail.expectLatestMetadata({notes:'Saved once'});await detail.cancelMetadata();await detail.expectMetadataClosed();await detail.expectSaved(item);expect(item.revision).toBe(2);
});
test('L2-043: an unavailable reference retains attempted metadata',async({page})=>{
  const {detail,inspiration}=await setup(page);inspiration.library.items=[];await detail.fillMetadata({notes:'Keep my draft'});await detail.saveMetadata();await detail.expectMetadataError('Reference unavailable. Your metadata draft has been kept.');await detail.expectDraft({notes:'Keep my draft'});await detail.expectMetadataDisabled();
});
test('L2-043: canceling and leaving protect an unsaved metadata draft',async({page})=>{
  const {detail,inspiration}=await setup(page);await detail.expectUnload(false);await detail.fillMetadata({notes:'Private draft'});await detail.expectUnload(true);
  await detail.cancelMetadata();await detail.expectMetadataDiscard();await detail.keepMetadata();await detail.expectDraft({notes:'Private draft'});
  await detail.returnToLibrary();await detail.expectMetadataDiscard();await detail.discardMetadata();await inspiration.expectOpen();await detail.expectUnload(false);
});
test('L2-043: unchanged metadata closes without a discard prompt',async({page})=>{
  const {detail}=await setup(page);await detail.cancelMetadata();await detail.expectMetadataClosed();
});
test('L2-043: pending metadata saves block repeat submission and show saved values after acknowledgment',async({page})=>{
  const {detail,inspiration,item}=await setup(page);inspiration.library.pause('update');await detail.fillMetadata({notes:'Pending draft'});await detail.saveMetadata();await detail.expectMetadataSaving();expect(item.notes).not.toBe('Pending draft');
  inspiration.library.release('update');await detail.expectMetadataClosed();await detail.expectSaved(item);expect(inspiration.library.updates).toHaveLength(1);await detail.expectUnload(false);
});
test('L2-043: discarded pending metadata cannot navigate after departure',async({page})=>{
  const {detail,inspiration}=await setup(page);inspiration.library.pause('update');await detail.fillMetadata({notes:'Late save'});await detail.saveMetadata();await detail.expectMetadataSaving();
  await detail.returnToLibrary();await detail.expectMetadataDiscard();await detail.discardMetadata();await inspiration.expectOpen();inspiration.library.release('update');await expect.poll(()=>inspiration.library.items[0].notes).toBe('Late save');await inspiration.expectOpen();
});
test('L2-037: sign-out clears a private metadata draft',async({page})=>{
  const {detail,work,signIn,inspiration,item}=await setup(page);await detail.fillMetadata({notes:'Private draft'});await work.signOut();await signIn.expectSignedOut();await signIn.continue();await inspiration.open();await inspiration.openReference(item.title);await detail.edit();await detail.expectDraft({notes:item.notes});await detail.expectUnload(false);
});
