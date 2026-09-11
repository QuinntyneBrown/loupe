import {test,expect} from '@playwright/test';
import {MyWorkPage} from '../page-objects/my-work-page.js';
import {PhotographerLibrary} from '../fixtures/photographer-library.js';
import {PhotographerPage} from '../page-objects/photographer-page.js';
async function setup(page){await new MyWorkPage(page).configureCollection(0);const library=new PhotographerLibrary(1);await library.attach(page);return {library,screen:new PhotographerPage(page)};}
test('pending summary polls to grounded suggestions while preserving the manual description',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.queued();await screen.open();await screen.expectSummaryState('Reading the site…');library.summaries.complete();await screen.expectSummaryState(library.summaries.value.summary);await screen.expectSummaryState('Live');await screen.expectSummarySource(library.items[0].source.fetchedUrl);expect(library.items[0].summary).toContain('Portraits in available light');
});
test('a failed summary can retry without replacing notes or the saved description',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.fail('invalid_output');await screen.open();await screen.expectSummaryState("Couldn't summarise the site this time");await screen.retrySummary();await screen.expectSummaryState('Reading the site…');expect(library.items[0].notes).toBe('Study the window light.');
});
for(const width of [1440,320])test(`blocked summary is honest and accessible at ${width}px`,async({page})=>{
 await page.setViewportSize({width,height:900});const {library,screen}=await setup(page);library.summaries.fail('robots_disallowed');await screen.open();await screen.expectSummaryState("doesn't allow automated reading");await screen.expectAccessible();
});
