import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';
import { ReferenceUploadPage } from '../page-objects/reference-upload-page.js';

async function setup(page) {
  const work = new MyWorkPage(page); await work.configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(0);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await inspiration.open(); const upload = new ReferenceUploadPage(page); await upload.open();
  return {work,inspiration,upload,signIn,detail:new ReferenceDetailPage(page)};
}
// Given an inspiration upload, when acknowledged, then exactly one private reference
// survives reload with supplied metadata and without a My Work entry or AI request.
for(const optional of [false,true]) test(`L2-009.1/2: save reference with optional metadata=${optional}`,async({page})=>{
  const {work,inspiration,upload,signIn,detail}=await setup(page);
  await upload.chooseImage();
  if(optional) await upload.fill({title:'  Morning light  ',sourceUrl:' https://source.example/photo ',attribution:'  Photographer  ',notes:' Study edges.\nKeep context. '});
  await upload.save(); await expect.poll(()=>inspiration.library.items.length).toBe(1);
  await detail.expectSaved(inspiration.library.items[0]); await page.reload(); await signIn.continue(); await detail.expectSaved(inspiration.library.items[0]);
  expect(inspiration.library.items[0].title).toBe(optional?'Morning light':'Morning');
  expect(work.library.photos).toHaveLength(0); expect(inspiration.library.calls.every(call=>['list','get','tags','upload'].includes(call))).toBe(true);
});
for(const [field,maximum] of [['title',200],['sourceUrl',2048],['attribution',200],['notes',10000]]) test(`L2-009.4: ${field} rejects excess characters before saving`,async({page})=>{
  const {inspiration,upload}=await setup(page); await upload.chooseImage(); await upload.fill({[field]:'x'.repeat(maximum+1)});
  await upload.expectFieldError(field,`Use ${maximum.toLocaleString('en-US')} characters or fewer.`); expect(inspiration.library.items).toHaveLength(0);
});
for(const sourceUrl of ['javascript:alert(1)','/relative','https://user:password@example.test','https://example.test:8443/path']) test(`L2-009.4: reject unsafe source ${sourceUrl}`,async({page})=>{
  const {upload}=await setup(page); await upload.chooseImage(); await upload.fill({sourceUrl});
  await upload.expectFieldError('sourceUrl','Use an HTTP or HTTPS URL without credentials or a nonstandard port.');
});
for(const [size,type,message] of [[0,'image/png','Choose an image that is not empty.'],[25000001,'image/png','Choose an image of 25 MB or less.'],[10,'image/gif','Choose a still JPEG, PNG, HEIC or WebP image.']]) test(`L2-009.4: reject image ${size}/${type}`,async({page})=>{
  const {upload}=await setup(page); await upload.chooseFile(size,type); await upload.expectFileError(message);
});
for(const lost of [false,true]) test(`L2-030: retry reference upload after lost acknowledgment=${lost}`,async({page})=>{
  const {inspiration,upload,detail}=await setup(page); if(lost) inspiration.library.lostUploadResponses=1; else inspiration.library.failures.upload=1;
  await upload.chooseImage(); await upload.fill({title:'Keep me',notes:'Keep my notes'}); await upload.save(); await upload.expectFailure(); await upload.expectDraft({title:'Keep me',notes:'Keep my notes'});
  await upload.retry(); await expect.poll(()=>inspiration.library.items.length).toBe(1); await detail.expectSaved(inspiration.library.items[0]); expect(inspiration.library.uploadReceipts.size).toBe(1);
});
test('L2-030: uncertain saved upload rejects changed metadata and resolves the original',async({page})=>{
  const {inspiration,upload,detail}=await setup(page); inspiration.library.lostUploadResponses=1;
  await upload.chooseImage(); await upload.fill({title:'Original'}); await upload.save(); await upload.expectFailure();
  await upload.fill({title:'Changed'}); await upload.retry(); await upload.expectFailure('This retry differs from an earlier upload. Restore the original file and fields, or return to Inspiration to start a new upload.');
  await upload.fill({title:'Original'}); await upload.retry(); await detail.expectSaved(inspiration.library.items[0]); expect(inspiration.library.items).toHaveLength(1);
});
test('L2-009.4: unreadable image requires reselection while retaining metadata',async({page})=>{
  const {inspiration,upload,detail}=await setup(page); inspiration.library.errors.upload=['file_unavailable'];
  await upload.chooseImage(); await upload.fill({notes:'Keep context'}); await upload.save(); await upload.expectReselection(); await upload.expectDraft({notes:'Keep context'});
  await upload.chooseImage(); await upload.retry(); await expect.poll(()=>inspiration.library.items.length).toBe(1); await detail.expectSaved(inspiration.library.items[0]);
});
test('L2-043: transfer progress waits for save acknowledgment and prevents double submission',async({page})=>{
  const {inspiration,upload,detail}=await setup(page); inspiration.library.pause('upload'); await upload.chooseImage(); await upload.save(); await upload.expectSaving();
  await inspiration.library.reportUploadProgress(page,{transferred:100,total:null}); await upload.expectProgress(100,null);
  await inspiration.library.reportUploadProgress(page,{transferred:100,total:400}); await upload.expectProgress(100,400);
  await inspiration.library.reportUploadProgress(page,{transferred:400,total:400}); await upload.expectSaving(); expect(inspiration.library.items).toHaveLength(0); expect(inspiration.library.calls.filter(call=>call==='upload')).toHaveLength(1);
  inspiration.library.release('upload'); await expect.poll(()=>inspiration.library.items.length).toBe(1); await detail.expectSaved(inspiration.library.items[0]);
});
test('L2-043: unsaved reference metadata offers Keep editing and Discard',async({page})=>{
  const {inspiration,upload}=await setup(page); await upload.expectUnload(false); await upload.fill({notes:'Private draft'}); await upload.expectUnload(true);
  await upload.leave(); await upload.expectDiscard(); await upload.keep(); await upload.expectDraft({notes:'Private draft'});
  await upload.leave(); await upload.expectDiscard(); await upload.discard(); await inspiration.expectOpen(); await upload.expectUnload(false);
});
test('L2-043: leaving a pending reference upload ignores its late acknowledgment',async({page})=>{
  const {inspiration,upload}=await setup(page); inspiration.library.pause('upload'); await upload.chooseImage(); await upload.save(); await upload.expectSaving();
  await upload.leave(); await upload.expectDiscard(); await upload.expectPendingWarning(); await upload.discard(); await inspiration.expectOpen();
  inspiration.library.release('upload'); await expect.poll(()=>inspiration.library.items.length).toBe(1); await inspiration.expectOpen();
});
