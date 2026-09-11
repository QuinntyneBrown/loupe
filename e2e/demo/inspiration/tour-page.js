import { expect } from '@playwright/test';
import { InspirationPage } from '../../page-objects/inspiration-page.js';
export class InspirationTourPage extends InspirationPage {
 async ready() {await this.expectOpen();await this.page.locator('article img').evaluateAll(images=>Promise.all(images.filter(i=>{const r=i.getBoundingClientRect();return r.top<innerHeight&&r.bottom>0}).map(i=>i.decode())));await this.page.evaluate(()=>document.fonts.ready);}
 async top() {await this.page.evaluate(()=>window.scrollTo(0,0));}
 async checkViewport() {expect(await this.page.evaluate(()=>document.documentElement.scrollWidth)).toBe(await this.page.evaluate(()=>innerWidth));}
 async revealNote(text) {await this.page.getByText(text,{exact:true}).scrollIntoViewIfNeeded();}
 async mobileScroll() {await this.page.evaluate(()=>window.scrollTo({top:600,behavior:'smooth'}));}
 async mock(url) {await this.page.goto(url);await this.page.locator('.lp-photo-grid[data-state="default"] img').evaluateAll(images=>Promise.all(images.map(i=>i.decode())));await this.page.evaluate(()=>document.fonts.ready);await this.page.locator('.lp-photo-grid[data-state="default"] article').first().hover();}
 async comparison(mock,current) {
 await this.page.setContent(`<html><style>body{margin:0;background:#f4f3ef;color:#26343a;font-family:Segoe UI}h1{font-size:30px;margin:32px}section{display:flex;gap:16px;margin:0 24px}article{width:50%;background:white;border:1px solid #d5d5cf}h2{font-size:22px;margin:18px}img{width:100%;display:block}p{font-size:22px;line-height:1.5;margin:24px 32px}strong{color:#255f65}</style><h1>Inspiration · mock and current implementation</h1><section><article><h2>Reference mock</h2><img src="data:image/png;base64,${mock}"></article><article><h2>Current Angular UI</h2><img src="data:image/png;base64,${current}"></article></section><p><strong>Shared:</strong> navigation, typography, image-led collection, save entry point.</p><p><strong>Mock only:</strong> boards sidebar and counts, tag filters, on-card board actions.</p><p><strong>Current UI:</strong> full-width cards with saved status and dates; source and notes on the detail page.</p></html>`);
 await this.page.locator('img').evaluateAll(images=>Promise.all(images.map(i=>i.decode())));
 }
}
