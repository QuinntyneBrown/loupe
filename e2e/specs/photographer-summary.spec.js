import {test,expect} from '@playwright/test';
import {MyWorkPage} from '../page-objects/my-work-page.js';
import {PhotographerLibrary} from '../fixtures/photographer-library.js';
import {PhotographerPage} from '../page-objects/photographer-page.js';
async function setup(page){await new MyWorkPage(page).configureCollection(0);const library=new PhotographerLibrary(1);await library.attach(page);return {library,screen:new PhotographerPage(page)};}
test('pending summary polls to grounded suggestions while preserving the manual description',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.queued();await screen.open();await screen.expectSummaryState('Reading the site…');library.summaries.complete();await screen.expectSuggestedSummary(library.summaries.value.summary);await screen.expectSummaryState('Live');await screen.expectSummarySource(library.items[0].source.fetchedUrl);expect(library.items[0].summary).toContain('Portraits in available light');
});
test('a failed summary can retry without replacing notes or the saved description',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.fail('invalid_output');await screen.open();await screen.expectSummaryState("Couldn't summarise the site this time");await screen.retrySummary();await screen.expectSummaryState('Reading the site…');expect(library.items[0].notes).toBe('Study the window light.');
});
for(const width of [1440,320])test(`blocked summary is honest and accessible at ${width}px`,async({page})=>{
 await page.setViewportSize({width,height:900});const {library,screen}=await setup(page);library.summaries.fail('robots_disallowed');await screen.open();await screen.expectSummaryState("doesn't allow automated reading");await screen.expectAccessible();
});
test('edited summary acceptance preserves notes and tags and Undo restores the previous description',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.complete();const original=library.items[0].summary;await screen.open();await screen.editSuggestedSummary('My edited summary');await screen.summaryAction('Accept summary');await screen.expectSummaryNotice('Summary accepted');expect(library.items[0].summary).toBe('My edited summary');expect(library.items[0].summaryProvenance).toBe('edited-ai');expect(library.items[0].notes).toBe('Study the window light.');expect(library.items[0].tags).toHaveLength(2);await screen.summaryAction('Undo');await screen.expectSuggestedSummary(library.summaries.value.summary);expect(library.items[0].summary).toBe(original);
});
test('edited suggested tags and bulk dismissal leave the saved summary unchanged',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.complete();const original=library.items[0].summary;await screen.open();await screen.editSummaryTag('soft light','Window practice','technique');await screen.summaryAction('Accept edited tag');await screen.expectTag('Window practice');expect(library.items[0].summary).toBe(original);await screen.summaryAction('Dismiss all');await screen.expectSummaryNotice('Suggestions dismissed');expect(library.items[0].summary).toBe(original);
});
test('failed and stale summary reviews retain edited text and newer notes',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.complete();library.summaries.failures.review=1;await screen.open();await screen.editSuggestedSummary('Retained draft');await screen.summaryAction('Accept summary');await screen.expectSummaryError("Couldn't save");await screen.expectSuggestedSummary('Retained draft');library.items[0].notes='Newer notes';library.items[0].revision++;await screen.summaryAction('Accept summary');await screen.expectSummaryError('changed');await screen.summaryAction('Review latest bookmark');await screen.expectSuggestedSummary('Retained draft');await screen.summaryAction('Accept summary');await screen.expectSummaryNotice('Summary accepted');expect(library.items[0].notes).toBe('Newer notes');
});
test('edited summary drafts participate in the page leave guard',async({page})=>{
 const {library,screen}=await setup(page);library.summaries.complete();await screen.open();await screen.editSuggestedSummary('Unsaved suggestion');await screen.backToCollection();await screen.keepEditing();await screen.expectSuggestedSummary('Unsaved suggestion');await screen.backToCollection();await screen.discardNotes();
});
