import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { InspirationPage } from '../page-objects/inspiration-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';

async function setup(page) {
  await new MyWorkPage(page).configureCollection(0);
  const inspiration = new InspirationPage(page); await inspiration.configure(25);
  inspiration.library.items[0].tags = [{ name: 'soft light' }];
  inspiration.library.items[24].tags = [{ name: 'soft light' }, { name: 'portrait' }];
  inspiration.library.items[1].tags = [{ name: 'motion blur' }];
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  await inspiration.open(); return { inspiration, signIn };
}

test('active tag filters use the whole library, combine with AND, persist in the URL and clear', async ({ page }) => {
  const { inspiration, signIn } = await setup(page);
  await inspiration.filterTag('soft light'); await inspiration.expectReferences(2);
  await inspiration.filterTag('portrait'); await inspiration.expectReferences(1);
  await inspiration.expectSelectedTag('soft light'); await inspiration.expectSelectedTag('portrait');
  await page.reload(); await signIn.continue(); await inspiration.expectReferences(1);
  await inspiration.expectSelectedTag('portrait');
  await inspiration.filterTag('motion blur'); await inspiration.expectFilteredEmpty();
  await inspiration.clearTags(); await inspiration.expectReferences(24);
});

test('mobile filters preview the count and only apply on Show references', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  const { inspiration } = await setup(page);
  await inspiration.openTagDialog(); await inspiration.selectDialogTag('portrait');
  await inspiration.closeTagDialog(); await inspiration.expectReferences(24);
  await inspiration.openTagDialog(); await inspiration.selectDialogTag('portrait');
  await inspiration.applyTagDialog(1); await inspiration.expectReferences(1);
});
