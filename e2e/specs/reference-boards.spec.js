import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { ReferenceDetailPage } from '../page-objects/reference-detail-page.js';

test('Reference detail saves board memberships and links to the resulting board', async ({ page }) => {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(1);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue(); await inspiration.open();
  await inspiration.newBoard('Window light'); await inspiration.openReference('Reference 01');
  const detail = new ReferenceDetailPage(page); await detail.openBoards(); await detail.selectPickerBoard('Window light');
  await inspiration.newPickerBoard('Portrait sittings'); await inspiration.saveBoardPicker(); await detail.expectBoardButtonFocus();
  await detail.expectBoardLink('Window light'); await detail.expectBoardLink('Portrait sittings');
  expect(inspiration.library.items[0].boardIds).toHaveLength(2);
  await detail.followBoard('Portrait sittings'); await inspiration.expectBoardTitle('Portrait sittings'); await inspiration.expectReferences(1);
});
