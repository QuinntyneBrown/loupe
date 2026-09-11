import { expect } from '@playwright/test';
import { PhotographDetailPage } from '../../page-objects/photograph-detail-page.js';
import { firstTitle, rockStatement } from './fixture.js';
export class CritiqueTourPage extends PhotographDetailPage {
  regeneration() { return this.page.getByRole('dialog', {name:`Regenerate “${firstTitle}”?`, exact:true}); }
  async ready() {
    await expect(this.page.getByRole('heading',{name:firstTitle,exact:true})).toBeVisible();
    await this.page.getByRole('img',{name:firstTitle,exact:true}).evaluate(image=>image.decode());
    await this.page.evaluate(()=>document.fonts.ready);
    await expect(this.page.getByRole('heading',{name:'What works',exact:true})).toBeVisible();
    await this.page.evaluate(()=>document.activeElement?.blur());
  }
  async top() { await this.page.evaluate(()=>window.scrollTo({top:0,behavior:'smooth'})); }
  async section(name) {
    const element=this.page.getByRole('heading',{name,exact:true}).first();
    await element.evaluate(el=>window.scrollTo({top:scrollY+el.getBoundingClientRect().top-90,behavior:'smooth'}));
  }
  async spotlightVisible() { await expect(this.page.locator('[data-evidence-region]')).toBeVisible(); }
  async notes(value, dry) {
    const editor=this.page.getByRole('textbox',{name:'Notes',exact:true});
    await editor.fill(''); await editor.pressSequentially(value,{delay:dry?0:30});
    await this.saveNotes(); await this.expectNotesState('Saved');
  }
  async menuClosed() {
    const more=this.page.getByRole('button',{name:'More actions',exact:true});
    if (await this.page.getByRole('button',{name:'Delete photograph',exact:true}).isVisible()) await more.click();
  }
  async checkViewport() {
    const sizes=await this.page.evaluate(()=>({viewport:innerWidth,document:document.documentElement.scrollWidth}));
    expect(sizes.document).toBe(sizes.viewport); return sizes;
  }
  async evidenceOnMobile() {
    await this.section('What works');
    await this.evidenceArea(rockStatement).waitFor({state:'visible'});
  }
}
