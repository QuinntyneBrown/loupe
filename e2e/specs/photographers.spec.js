import { test, expect } from '@playwright/test';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { PhotographersPage } from '../page-objects/photographers-page.js';

async function setup(page,count) {
  await new MyWorkPage(page).configureCollection(0);
  const photographers = new PhotographersPage(page); await photographers.configure(count);
  const signIn = new SignInPage(page); await signIn.openPrivateDestination(); await signIn.continue();
  return photographers;
}

test('bookmarks show safe portfolio links and page without losing earlier cards on retry',async ({page}) => {
  const photographers = await setup(page,25); await photographers.open();
  await photographers.expectCount(25); await photographers.expectCards(24);
  await photographers.expectCard('Photographer 01','portfolio1.example',6);
  await photographers.expectCard('Photographer 02','portfolio2.example',0);
  photographers.library.failures=1; await photographers.more(); await photographers.expectError(); await photographers.expectCards(24);
  await photographers.retry(); await photographers.expectCards(25);
  await photographers.expectCardFocused('Photographer 25');
  expect(photographers.library.calls.map(call=>call.operation)).toEqual(['list','list','list']);
});

test('empty and failed collections can recover without fetching portfolios',async ({page}) => {
  const photographers = await setup(page,0); photographers.library.failures=1; await photographers.open();
  await photographers.expectError(); await photographers.retry(); await photographers.expectEmpty();
  await photographers.expectEmptyFocused();
  await photographers.expectAccessible();
});

for (const width of [1440,768,375]) test(`photographer cards are accessible without overflow at ${width}px`,async ({page}) => {
  await page.setViewportSize({width,height:900}); const photographers=await setup(page,7); await photographers.open();
  await photographers.expectCards(7); await photographers.expectAccessible();
});
