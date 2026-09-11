import { expect } from '@playwright/test';
export class ReferenceMockPage {
  constructor(page) { this.page=page; }
  async open() {
    await this.page.goto('http://127.0.0.1:8765/critique.html?chrome=0');
    await this.page.locator('.lp-detail__media img').evaluate(image=>image.decode());
    await this.page.evaluate(()=>document.fonts.ready);
    await expect(this.page.getByRole('heading',{name:'What works',exact:true})).toBeVisible();
  }
}
