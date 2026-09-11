import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';
import { critiqueOperation } from '../fixtures/critique-operation.js';

for (const width of [375, 1440]) {
  test(`L2-053.2-3: critique follows the reference layout at ${width}px`, async ({ page }, info) => {
    // Given a saved photograph with a completed critique and editable personal context.
    await page.setViewportSize({ width, height: 900 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const work = new MyWorkPage(page);
    await work.configureCollection(1);
    const id = work.library.photos[0].id;
    const critique = savedCritique();
    work.library.critiques.set(id, critique);
    work.library.critiqueOperations.set(id, {
      ...critiqueOperation(id, 'Succeeded'),
      id: critique.operationId,
      completedAt: critique.generatedAt,
      message: 'Critique saved.',
    });
    const signIn = new SignInPage(page);
    await signIn.openPrivateDestination();
    await signIn.continue();

    // When detail opens, the brief stays with the image and critique sections follow the mock.
    await work.openPhotograph('Study 01');
    const detail = new PhotographDetailPage(page);
    await detail.expectCritiqueText('The shape communicates the intended quiet mood.');
    await detail.expectMockCritiqueHierarchy();
    await detail.expectMockMediaAndBriefLayout(width >= 992);
    await detail.expectHeaderCompareDestination(id);
    await detail.expectContentFitsViewport('Study 01');
    await detail.capture(info.outputPath(`critique-mock-${width}.png`));

    // Then secondary actions are available from the menu and comparison keeps this photograph selected.
    await detail.expectDetailActionsMenu();
    await detail.followHeaderCompare(id);
  });
}
