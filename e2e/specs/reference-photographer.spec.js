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
