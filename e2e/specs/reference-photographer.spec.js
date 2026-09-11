import {test,expect} from '@playwright/test';
import {MyWorkPage} from '../page-objects/my-work-page.js';
import {SignInPage} from '../page-objects/sign-in-page.js';
import {InspirationPage} from '../page-objects/inspiration-page.js';
import {ReferenceDetailPage} from '../page-objects/reference-detail-page.js';
import {PhotographerLibrary} from '../fixtures/photographer-library.js';
async function setup(page){await new MyWorkPage(page).configureCollection(0);const inspiration=new InspirationPage(page);await inspiration.configure(1);const photographers=new PhotographerLibrary(26);await photographers.attach(page);inspiration.library.photographerLibrary=photographers;const signIn=new SignInPage(page);await signIn.openPrivateDestination();await signIn.continue();await inspiration.open();await inspiration.openReference('Reference 01');return {references:inspiration.library,photographers,screen:new ReferenceDetailPage(page)};}
test('reference picker searches all photographers, links and changes without altering attribution',async ({page})=>{
 const {screen,references}=await setup(page);await screen.linkPhotographer();await screen.searchPhotographers('Photographer 26');await screen.choosePhotographer('Photographer 26');await screen.confirmPhotographer();await screen.expectPhotographer('Photographer 26','photographer-26');expect(references.items[0].attribution).toBe('Supplied photographer');await screen.changePhotographer();await screen.choosePhotographer('Photographer 01');await screen.confirmPhotographer();await screen.expectPhotographer('Photographer 01','photographer-1');
});
test('canceling a photographer choice leaves the reference unchanged',async ({page})=>{
 const {screen,references}=await setup(page);await screen.linkPhotographer();await screen.choosePhotographer('Photographer 01');await screen.cancelPhotographer();expect(references.calls).not.toContain('setPhotographer');
});
test('stale reference review preserves the selected photographer and newer metadata',async ({page})=>{
 const {screen,references}=await setup(page);await screen.linkPhotographer();await screen.choosePhotographer('Photographer 01');references.items[0].notes='Newer notes';references.items[0].revision++;await screen.confirmPhotographer();await screen.expectPhotographerFailure();await screen.reviewPhotographer();await screen.confirmPhotographer();await screen.expectPhotographer('Photographer 01','photographer-1');expect(references.items[0].notes).toBe('Newer notes');
});
test('a missing photographer can be replaced by another choice',async ({page})=>{
 const {screen,photographers}=await setup(page);await screen.linkPhotographer();await screen.choosePhotographer('Photographer 01');photographers.items.shift();await screen.confirmPhotographer();await screen.expectPhotographerFailure();await screen.choosePhotographer('Photographer 02');await screen.confirmPhotographer();await screen.expectPhotographer('Photographer 02','photographer-2');
});
test('cancel after reviewing a stale reference displays its latest notes',async ({page})=>{
 const {screen,references}=await setup(page);await screen.linkPhotographer();await screen.choosePhotographer('Photographer 01');references.items[0].notes='New notes from another tab';references.items[0].revision++;await screen.confirmPhotographer();await screen.reviewPhotographer();await screen.cancelPhotographer();await screen.expectText('notes','New notes from another tab');
});
test('retry reconciles a completed link whose response was lost',async ({page})=>{
 const {screen,references}=await setup(page);references.lostPhotographerResponses=1;await screen.linkPhotographer();await screen.choosePhotographer('Photographer 01');await screen.confirmPhotographer();await screen.expectPhotographerFailure();await screen.confirmPhotographer();await screen.expectPhotographer('Photographer 01','photographer-1');expect(references.items[0].revision).toBe(2);
});
for(const width of [1280,320])test(`photographer picker remains accessible at ${width}px`,async ({page})=>{
 await page.setViewportSize({width,height:800});const {screen}=await setup(page);await screen.linkPhotographer();await screen.choosePhotographer('Photographer 01');await screen.expectSuggestionsAccessible();
});
