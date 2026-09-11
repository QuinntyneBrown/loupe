import { expect } from '@playwright/test';
export class PlayerPage {
 constructor(page) {this.page=page;this.video=page.locator('video');}
 async metadata() {await this.video.evaluate(v=>v.readyState>=1?undefined:new Promise((resolve,reject)=>{v.onloadedmetadata=resolve;v.onerror=()=>reject(new Error('Video failed to load'));}));return this.video.evaluate(v=>({duration:v.duration,width:v.videoWidth,height:v.videoHeight}));}
 async play() {await this.video.evaluate(async v=>{v.muted=true;await v.play();});await expect.poll(()=>this.video.evaluate(v=>v.currentTime)).toBeGreaterThan(.5);await this.video.evaluate(v=>v.pause());}
 async chapter(chapter) {await this.page.getByRole('button',{name:chapter.title,exact:true}).click();await this.video.evaluate(v=>new Promise(resolve=>{if(!v.seeking)resolve();else v.addEventListener('seeked',resolve,{once:true});}));await this.video.evaluate(v=>v.pause());expect(Math.abs(await this.video.evaluate(v=>v.currentTime)-chapter.start)).toBeLessThan(2);}
}
