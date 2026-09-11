import { chromium } from 'playwright';
import { pathToFileURL } from 'node:url';
import { readFileSync,writeFileSync } from 'node:fs';
const root=process.cwd();
const browser=await chromium.launch();
const page=await browser.newPage({viewport:{width:1440,height:1100}});
const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.goto(pathToFileURL(root+'/docs/demo/critique/index.html').href);
await page.locator('video').evaluate(v=>new Promise((resolve,reject)=>{if(v.readyState>=1)return resolve();v.onloadedmetadata=resolve;v.onerror=()=>reject(v.error.message)}));
const metadata=await page.locator('video').evaluate(async v=>{v.muted=true;await v.play();await new Promise(r=>setTimeout(r,1100));v.pause();return {duration:v.duration,width:v.videoWidth,height:v.videoHeight,playedTo:v.currentTime,error:v.error?.message}});
if(metadata.width!==1440||metadata.height!==1040||metadata.duration<170||metadata.playedTo<.5)throw Error(JSON.stringify(metadata));
const chapters=JSON.parse(readFileSync('docs/demo/critique/chapters.json'));
for(const chapter of [chapters[2],chapters[8],chapters[10]]){
 await page.getByRole('button',{name:chapter.title,exact:true}).click();
 await page.waitForTimeout(400);
 const time=await page.locator('video').evaluate(v=>{v.pause();return v.currentTime});
 if(Math.abs(time-chapter.start)>2)throw Error('Chapter seek failed');
}
await page.screenshot({path:'docs/demo/critique/source/player.png'});
if(errors.length)throw Error(errors.join('\n'));
writeFileSync('docs/demo/critique/source/playback-check.json',JSON.stringify({metadata,chaptersChecked:3,errors},null,2));
console.log(JSON.stringify(metadata));
await browser.close();
