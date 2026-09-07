import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';

const viewports = [320, 375, 575, 576, 767, 768, 991, 992, 1199, 1200, 1440, 1920]
  .map(width => ({ width, height: 900 })).concat([{ width: 375, height: 667 }, { width: 844, height: 390 }]);

for (const viewport of viewports) {
  test(`L2-004/044/045/046: notes recovery at ${viewport.width}x${viewport.height}`, async ({ page }, info) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();
    await work.openPhotograph('Study 01');
    const detail = new PhotographDetailPage(page);
    await detail.editNotes('Keep space around the subject.');
    work.library.failures.updateNotes = 1;
    await detail.saveNotes();
    await detail.expectNotesFailure();
    await detail.expectNotes('Keep space around the subject.');
    await detail.expectAccessibleNotes();
    if (info.project.name === 'chromium' && [375, 1440].includes(viewport.width))
      await detail.capture(info.outputPath('notes-error.png'));
    await detail.retryNotes();
    await detail.expectNotesState('Saved');
    await detail.editNotes('a'.repeat(10001));
    await detail.expectNotesLimit();
    await detail.expectAccessibleNotes();
  });
}
