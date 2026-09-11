// Acceptance tests. Traces to L2-017, L2-018, L2-019, L2-030, L2-044.
import { test } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';

async function setup(page, count = 3) {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(count);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await inspiration.open();
  return { inspiration, signIn };
}

test('create boards, preserve a duplicate name draft, and reopen saved boards', async ({ page }) => {
  const { inspiration, signIn } = await setup(page);
  await inspiration.newBoard('Window light');
  await inspiration.expectBoard('Window light', 0);
  await inspiration.newBoard(' window LIGHT ');
  await inspiration.expectBoardError('A board with this name already exists. Choose another name.');
  await inspiration.cancelBoardDialog();
  await inspiration.newBoard('Colour studies');
  await inspiration.expectBoard('Colour studies', 0);
  await page.reload(); await signIn.continue();
  await inspiration.expectBoard('Window light', 0);
  await inspiration.expectBoard('Colour studies', 0);
});

test('board membership counts, filtered views and remove Undo preserve references', async ({ page }) => {
  const { inspiration } = await setup(page);
  await inspiration.newBoard('Window light');
  await inspiration.newBoard('Portrait sittings');
  await inspiration.assignBoards('Reference 01', ['Window light', 'Portrait sittings']);
  await inspiration.expectBoard('Window light', 1); await inspiration.expectBoard('Portrait sittings', 1);
  await inspiration.selectBoard('Window light'); await inspiration.expectBoardTitle('Window light');
  await inspiration.expectReferences(1);
  await inspiration.removeFromBoard('Reference 01'); await inspiration.expectReferences(0);
  await inspiration.expectBoard('Window light', 0); await inspiration.expectBoard('Portrait sittings', 1);
  await inspiration.undoRemoval(); await inspiration.expectReferences(1);
  await inspiration.selectBoard('All references'); await inspiration.expectReferences(3);
});

test('rename and delete a board without deleting the references on it', async ({ page }) => {
  const { inspiration } = await setup(page);
  await inspiration.newBoard('Window light');
  await inspiration.assignBoards('Reference 01', ['Window light']);
  await inspiration.selectBoard('Window light');
  await inspiration.renameBoard('Soft light'); await inspiration.expectBoardTitle('Soft light');
  await inspiration.deleteBoard('Soft light', false); await inspiration.expectBoard('Soft light', 1);
  await inspiration.deleteBoard('Soft light', true); await inspiration.expectBoardTitle('Inspiration');
  await inspiration.expectReferences(3);
});

test('assigning a board on a later page preserves the loaded library and keyboard position', async ({ page }) => {
  const { inspiration } = await setup(page, 25);
  await inspiration.newBoard('Window light');
  await inspiration.loadMore();
  await inspiration.assignBoards('Reference 25', ['Window light']);
  await inspiration.expectBoard('Window light', 1);
  await inspiration.expectReferences(25);
  await inspiration.expectBoardActionFocus('Reference 25');
});

test('inline board creation survives a membership failure and retries without duplicate creation', async ({ page }) => {
  const { inspiration } = await setup(page);
  await inspiration.openBoardPicker('Reference 01');
  await inspiration.newPickerBoard('Window light');
  inspiration.library.failures['boards-setMemberships'] = 1;
  await inspiration.saveBoardPicker();
  await inspiration.expectBoardError('Board changes could not be saved. Your selection is still here. Try again.');
  await inspiration.expectPickerSelection('Window light');
  await inspiration.saveBoardPicker();
  await inspiration.expectBoard('Window light', 1);
  await inspiration.selectBoard('Window light');
  await inspiration.expectReferences(1);
});
