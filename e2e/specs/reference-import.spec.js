import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';
async function setup(page,configure=()=>{}) {
 const work=new MyWorkPage(page);await work.configureCollection(0);const inspiration=new InspirationPage(page);await inspiration.configure(1);configure(inspiration.library);
 const signIn=new SignInPage(page);await signIn.openPrivateDestination();await signIn.continue();await inspiration.open();await inspiration.openReference('Reference 01');
 return {library:inspiration.library,inspiration,signIn,detail:new ReferenceDetailPage(page)};
}
// Given a saved source, when import is explicitly admitted, then status is durable
// and visible after reload while the saved reference remains readable.
test('L2-010/033: request import and recover its current operation on reload',async({page})=>{
 const {library,detail,signIn}=await setup(page);await detail.expectImportReady();expect(library.importCalls.filter(call=>call.operation==='request')).toHaveLength(0);
 await detail.requestImport();await detail.expectImportState('Queued');await detail.expectImportFocused();await detail.expectImportMessage('Demo');await detail.expectSaved(library.items[0]);
 await page.reload();await signIn.continue();await detail.expectImportState('Queued');expect(library.importReceipts.size).toBe(1);
});
test('L2-010: missing source has an honest manual action',async({page})=>{
 const {detail}=await setup(page,library=>library.items[0].sourceUrl=null);await detail.expectNoImportSource();await detail.edit();await detail.fillMetadata({sourceUrl:'https://source.example/new'});await detail.saveMetadata();await detail.expectImportReady();
});
test('L2-043: unsaved metadata blocks a new import until saved or canceled',async({page})=>{
 const {detail}=await setup(page);await detail.expectImportReady();await detail.edit();await detail.fillMetadata({notes:'Unsaved'});await detail.expectImportDisabled();await detail.saveMetadata();await detail.expectImportReady();
});
for(const error of ['request_failed','analysis_limit','integration_not_configured']) test(`L2-030/035/036: recover import admission ${error}`,async({page})=>{
 const {library,detail}=await setup(page);library.importErrors.request=[error];await detail.requestImport();await detail.expectImportError(error==='analysis_limit'?'Too many operations':error==='integration_not_configured'?'Import is not configured':'Import admission was not confirmed');await detail.expectSaved(library.items[0]);await detail.retryImportAdmission();await detail.expectImportState('Queued');
 const calls=library.importCalls.filter(call=>call.operation==='request');expect(calls[0].operationKey).toBe(calls[1].operationKey);
});
test('L2-030: lost admission response resolves the same job',async({page})=>{
 const {library,detail}=await setup(page);library.lostImportResponses=1;await detail.requestImport();await detail.expectImportError('Import admission was not confirmed');await detail.retryImportAdmission();await detail.expectImportState('Queued');expect(library.importReceipts.size).toBe(1);
});
test('L2-030: stale source requires explicit review before a fresh request',async({page})=>{
 const {library,detail}=await setup(page);await detail.expectImportReady();library.items[0].sourceUrl='https://source.example/changed';library.items[0].revision++;
 await detail.requestImport();await detail.expectImportError('Review its latest saved source');await detail.expectImportDisabled();await detail.reviewImportSource();await detail.expectReviewedImportSource('https://source.example/changed');await detail.requestImport();await detail.expectImportState('Queued');
 const calls=library.importCalls.filter(call=>call.operation==='request');expect(calls[1].revision).toBe(2);expect(calls[1].operationKey).not.toBe(calls[0].operationKey);
});
test('L2-033: poll persisted progress and refresh completed reference without discarding a metadata draft',async({page})=>{
 const {library,detail}=await setup(page);await detail.requestImport();await detail.expectImportState('Queued');const operation=library.importOperations.get(library.items[0].id);operation.status='Running';operation.message='Fetching permitted source.';await detail.expectImportState('Running');
 await detail.edit();await detail.fillMetadata({notes:'My unsaved draft'});operation.status='Succeeded';operation.message='Import complete.';library.items[0].attribution='Fetched author';library.items[0].revision++;
 await detail.expectImportState('Succeeded');await detail.expectSaved(library.items[0]);await detail.expectDraft({notes:'My unsaved draft'});
});
test('L2-033: failed status read retains reference and recovers focus',async({page})=>{
 const {library,detail}=await setup(page,library=>library.importFailures.current=1);await detail.expectImportError('Import status could not be loaded');await detail.expectSaved(library.items[0]);await detail.retryImportStatus();await detail.expectImportReady();await detail.expectImportFocused();
});
test('L2-043: leaving pending admission ignores the late response',async({page})=>{
 const {library,detail,inspiration}=await setup(page);await detail.expectImportReady();library.pause('import-request');await detail.requestImport();await detail.expectImportDisabled();await detail.returnToLibrary();await inspiration.expectOpen();library.release('import-request');await expect.poll(()=>library.importReceipts.size).toBe(1);await inspiration.expectOpen();
});
test('L2-011/033: failed import keeps source and manual metadata usable',async({page})=>{
 const {library,detail}=await setup(page);await detail.requestImport();await detail.expectImportState('Queued');Object.assign(library.importOperations.get(library.items[0].id),{status:'Failed',failureCode:'robots_disallowed',message:'This source disallows automated fetching. Your link is saved.'});await detail.expectImportState('Failed');await detail.expectImportMessage('Your link is saved');await detail.expectSaved(library.items[0]);await detail.edit();await detail.fillMetadata({notes:'Continue manually'});await detail.saveMetadata();
});

test('L2-043: pending import acknowledgment preserves focus moved into metadata',async({page})=>{
 const {library,detail}=await setup(page);await detail.expectImportReady();library.pause('import-request');await detail.requestImport();await detail.edit();await detail.fillMetadata({notes:'Keep focus here'});library.release('import-request');await detail.expectImportState('Queued');await detail.expectMetadataFieldFocused('notes');
});
test('L2-033: initial status read remains single flight while pending',async({page})=>{
 const {library,detail}=await setup(page,library=>library.pause('import-current'));await expect.poll(()=>library.importCalls.length).toBe(1);await detail.expectImportMessage('Loading import status');await page.clock.install();await page.clock.fastForward(12000);expect(library.importCalls).toHaveLength(1);library.release('import-current');await detail.expectImportReady();
});

test('L2-033: completed import with failed reference refresh preserves content and retries',async({page})=>{
 const {library,detail}=await setup(page);await detail.requestImport();await detail.expectImportState('Queued');library.failures.get=1;Object.assign(library.importOperations.get(library.items[0].id),{status:'Succeeded',message:'Import complete.'});await detail.expectImportError('updated reference could not be loaded');await detail.expectSaved(library.items[0]);await detail.retryImportStatus();await detail.expectImportState('Succeeded');await detail.expectImportFocused();
});
test('L2-033: failed polling retains the displayed operation and retries',async({page})=>{
 const {library,detail}=await setup(page);await detail.requestImport();await detail.expectImportState('Queued');library.importFailures.current=1;await detail.expectImportError('Import status could not be loaded');await detail.expectImportState('Queued');Object.assign(library.importOperations.get(library.items[0].id),{status:'Failed',message:'Source unavailable. Your link is saved.'});await detail.retryImportStatus();await detail.expectImportState('Failed');await detail.expectSaved(library.items[0]);
});
