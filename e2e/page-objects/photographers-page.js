import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { PhotographerLibrary } from '../fixtures/photographer-library.js';
import { SignInPage } from './sign-in-page.js';

export class PhotographersPage {
  constructor(page) {this.page = page;}
  async configure(count) {this.library = new PhotographerLibrary(count); await this.library.attach(this.page);}
  async open() {await this.page.goto('/photographers'); await new SignInPage(this.page).continue();}
  async expectCount(count) {await expect(this.page.getByText(`${count} bookmarked`,{exact:true})).toBeVisible();}
  async expectCards(count) {await expect(this.page.getByRole('article')).toHaveCount(count);}
  async expectCard(name, domain, count) {
    const card = this.page.getByRole('article').filter({has:this.page.getByRole('heading',{name,exact:true})});
    await expect(card.getByRole('link',{name:`Open ${name}`,exact:true})).toHaveAttribute('href',/\/photographers\//);
    const source = card.getByRole('link',{name:`Open ${domain}`,exact:true});
    await expect(source).toHaveAttribute('target','_blank'); await expect(source).toHaveAttribute('rel','noopener noreferrer'); await expect(source).toHaveAttribute('referrerpolicy','no-referrer');
    await expect(card.getByText(count ? `${count} references` : 'No references yet',{exact:true})).toBeVisible();
  }
  async more() {await this.page.getByRole('button',{name:'Load more',exact:true}).click();}
  async retry() {await this.page.getByRole('button',{name:'Try again',exact:true}).click();}
  async expectError() {await expect(this.page.getByRole('heading',{name:"Couldn't load your photographers.",exact:true})).toBeVisible();}
  async expectEmpty() {await expect(this.page.getByRole('heading',{name:'Bookmark the photographers you learn from.',exact:true})).toBeVisible();}
  async expectAccessible() {
    expect((await new AxeBuilder({page:this.page}).withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze()).violations).toEqual([]);
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  }
}
