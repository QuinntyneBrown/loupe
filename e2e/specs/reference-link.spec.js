import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';
import { ReferenceLinkPage } from '../page-objects/reference-link-page.js';
async function setup(page,count=0) {
 const work=new MyWorkPage(page);await work.configureCollection(0);
 const inspiration=new InspirationPage(page);await inspiration.configure(count);
 const signIn=new SignInPage(page);await signIn.openPrivateDestination();await signIn.continue();await inspiration.open();
 const link=new ReferenceLinkPage(page);await link.open();return {work,inspiration,signIn,link,detail:new ReferenceDetailPage(page)};
}
// Given a source and optional manual context, when the save is acknowledged,
// then a durable link-only reference opens without requesting external analysis.
for(const optional of [false,true]) test(`L2-010: save source with optional context=${optional}`,async({page})=>{
 const {work,inspiration,signIn,link,detail}=await setup(page);await link.fill({sourceUrl:' https://source.example/study#original ',...(optional?{title:' Study ',attribution:' Artist ',notes:' Keep this. '}: {})});await link.save();
 await expect.poll(()=>inspiration.library.items.length).toBe(1);await detail.expectSaved(inspiration.library.items[0]);await page.reload();await signIn.continue();await detail.expectSaved(inspiration.library.items[0]);
 expect(inspiration.library.items[0].title).toBe(optional?'Study':'source.example');expect(inspiration.library.items[0].imageUrl).toBeNull();expect(work.library.photos).toHaveLength(0);expect(inspiration.library.calls.every(call=>['list','get','tags','saveLink'].includes(call))).toBe(true);
});
test('L2-010.4: duplicate source preserves saved metadata and retains the submitted draft',async({page})=>{
 const {inspiration,link,detail}=await setup(page,1);const original={...inspiration.library.items[0]};await link.fill({sourceUrl:'HTTPS://SOURCE.example:443/photo#fragment',title:'New attempted title',notes:'New attempted note'});await link.save();await link.expectDuplicate();await link.expectDraft({title:'New attempted title',notes:'New attempted note'});expect(inspiration.library.items).toEqual([original]);await link.openExisting();await detail.expectSaved(original);
});
for(const lost of [false,true]) test(`L2-030: link retry recovers uncertain response=${lost}`,async({page})=>{
 const {inspiration,link,detail}=await setup(page);if(lost) inspiration.library.lostLinkResponses=1;else inspiration.library.failures.saveLink=1;
 await link.fill({sourceUrl:'https://source.example/study',notes:'Keep context'});await link.save();await link.expectFailure();await link.expectDraft({notes:'Keep context'});await link.retry();await expect.poll(()=>inspiration.library.items.length).toBe(1);
 if(lost) {await link.expectDuplicate();await link.openExisting();}await detail.expectSaved(inspiration.library.items[0]);expect(inspiration.library.linkReceipts.size).toBe(1);
});
for(const sourceUrl of ['', 'javascript:alert(1)','/relative','https://user:pass@example.test','https://example.test:8443']) test(`L2-010: reject source ${sourceUrl}`,async({page})=>{
 const {link,inspiration}=await setup(page);await link.fill({sourceUrl});await link.expectDisabled();if(sourceUrl) await link.expectFieldError('sourceUrl','Use an HTTP or HTTPS URL without credentials or a nonstandard port.');expect(inspiration.library.items).toHaveLength(0);
});
for(const [field,maximum] of [['title',200],['sourceUrl',2048],['attribution',200],['notes',10000]]) test(`L2-010: link ${field} length validation`,async({page})=>{
 const {link}=await setup(page);await link.fill({sourceUrl:'https://source.example/study',[field]:'x'.repeat(maximum+1)});await link.expectFieldError(field,`Use ${maximum.toLocaleString('en-US')} characters or fewer.`);
});
test('L2-043: link draft navigation can be kept or discarded',async({page})=>{
 const {link,inspiration}=await setup(page);await link.expectUnload(false);await link.fill({notes:'Draft'});await link.expectUnload(true);await link.leave();await link.expectDiscard();await link.keep();await link.expectDraft({notes:'Draft'});await link.leave();await link.discard();await inspiration.expectOpen();await link.expectUnload(false);
});
test('L2-043: pending link save is single flight and cannot navigate after departure',async({page})=>{
 const {link,inspiration}=await setup(page);inspiration.library.pause('saveLink');await link.fill({sourceUrl:'https://source.example/study'});await link.save();await link.expectSaving();expect(inspiration.library.calls.filter(call=>call==='saveLink')).toHaveLength(1);await link.leave();await link.expectDiscard();await link.expectPendingWarning();await link.discard();inspiration.library.release('saveLink');await expect.poll(()=>inspiration.library.items.length).toBe(1);await inspiration.expectOpen();
});
