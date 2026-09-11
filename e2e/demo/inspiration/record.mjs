import { chromium } from 'playwright';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { expect } from '@playwright/test';
import { SignInPage } from '../../page-objects/sign-in-page.js';
import { InspirationTourPage } from './tour-page.js';
import { ReferenceDetailPage } from '../../page-objects/reference-detail-page.js';
import { ReferenceLinkPage } from '../../page-objects/reference-link-page.js';
import { PhotographLibrary } from '../../fixtures/photograph-library.js';
import { makeLibrary } from './fixture.js';

const root=fileURLToPath(new URL('../../..',import.meta.url));
const output=path.join(root,'docs/demo/inspiration/source');
mkdirSync(output,{recursive:true});
const dry=process.argv.includes('--dry');
const scenes=JSON.parse(readFileSync(dry?new URL('./scenes.json',import.meta.url):path.join(output,'narration.json')));
const browser=await chromium.launch();
const library=makeLibrary(); const photos=new PhotographLibrary(0);
const app=process.env.APP_URL||'http://127.0.0.1:4207'; const mockUrl=process.env.MOCK_URL||'http://127.0.0.1:8765/inspiration.html?chrome=0';
const errors=[]; const unexpectedRequests=[]; const timeline=[]; const recordings=[];
const pause=ms=>new Promise(resolve=>setTimeout(resolve,dry?Math.min(ms,100):ms));
async function createCapture(name,width=1440) {
  const context=await browser.newContext({viewport:{width,height:900},baseURL:app,recordVideo:dry?undefined:{dir:output,size:{width,height:900}}});
  await context.route('https://picsum.photos/**',route=>route.fulfill({body:readFileSync(new URL('./images/'+new URL(route.request().url()).pathname.split('/')[2]+'.jpg',import.meta.url)),contentType:'image/jpeg'}));
  await context.route('**/*',async route=>{
    const url=route.request().url();
    if (/^https?:/.test(url) && !/^https?:\/\/(127\.0\.0\.1|localhost)(:|\/)/.test(url) && !/^https:\/\/(picsum.photos|fonts.googleapis.com|fonts.gstatic.com)\//.test(url)) {
      unexpectedRequests.push(url.split('?')[0]); await route.abort();
    } else await route.fallback();
  });
  await context.addInitScript(()=>{
    document.addEventListener('DOMContentLoaded',()=>{
      const pointer=document.createElement('div');
      pointer.setAttribute('aria-hidden','true');
      pointer.style.cssText='position:fixed;width:14px;height:14px;border:2px solid white;border-radius:50%;background:#2563eb99;box-shadow:0 0 0 1px #2563eb;pointer-events:none;z-index:2147483647;display:none';
      document.body.appendChild(pointer);
      document.addEventListener('pointermove',event=>{pointer.style.display='block';pointer.style.left=(event.clientX-7)+'px';pointer.style.top=(event.clientY-7)+'px';});
    });
  });
  const page=await context.newPage(); page.setDefaultTimeout(15000);
  page.on('pageerror',error=>errors.push(error.message));
  await library.attach(page); await photos.attach(page);
  await page.setContent('<body style="margin:0;background:#ff00ff"></body>');
  await pause(2000);
  await page.evaluate(()=>document.body.style.background='#ffffff');
  const anchor=Date.now();
  const video=page.video();
  return {name,context,page,anchor,video,width};
}
async function openApp(capture) {
 await capture.page.goto(app+'/inspiration');
 await new SignInPage(capture.page).continue();
 await new InspirationTourPage(capture.page).ready();
}
async function scene(capture,id,action) {
  const specification=scenes.find(s=>s.id===id);
  const start=Date.now();
  console.log('Scene:',id);
  await action();
  const duration=specification.duration??12;
  await pause(Math.max(0,duration*1000-(Date.now()-start)));
  const end=Date.now();
  await capture.page.screenshot({path:path.join(output,`${dry?'preview':'frame'}-${id}.png`)});
  timeline.push({id,title:specification.title,start:(start-capture.anchor)/1000,end:(end-capture.anchor)/1000,capture:capture.name,width:capture.width});
}
async function closeCapture(capture) {
  await capture.context.close();
  if(capture.video) { const filename=path.join(output,capture.name+'.webm'); await capture.video.saveAs(filename); recordings.push({name:capture.name,filename,width:capture.width}); }
}
try {
 const desktop=await createCapture('desktop');const page=desktop.page;const tour=new InspirationTourPage(page);const detail=new ReferenceDetailPage(page);
 await tour.mock(mockUrl);
 const mock=(await page.screenshot({path:path.join(output,'mock.png')})).toString('base64');
 await scene(desktop,'reference',async()=>{});
 await openApp(desktop);await tour.expectReferences(24);
 const current=(await page.screenshot({path:path.join(output,'current.png')})).toString('base64');
 await scene(desktop,'layout',async()=>{});
 await tour.comparison(mock,current);
 await scene(desktop,'comparison',async()=>{});
 await openApp(desktop);
 await scene(desktop,'browse',async()=>{await pause(1800);await tour.loadMore();await tour.expectReferences(25);await pause(4500);await tour.top();});
 await tour.openReference(library.items[0].title);await detail.expectSaved(library.items[0]);
 await scene(desktop,'detail',async()=>{await pause(3500);await detail.edit();await detail.fillMetadata({notes:'Study the light and separation. Keep the source context.'});await pause(3500);await detail.saveMetadata();await detail.expectMetadataClosed();await detail.expectSaved(library.items[0]);await tour.revealNote(library.items[0].notes);});
 await detail.returnToLibrary();const link=new ReferenceLinkPage(page);await link.open();await link.expectOpen();
 await scene(desktop,'link',async()=>{await pause(1500);await link.fill({title:'Window light study',sourceUrl:'https://source.example/window-light',attribution:'Demonstration source',notes:'Return to study how light shapes the subject.'});await pause(4500);await link.save();await detail.expectSaved(library.items[0]);});
 library.failures.list=1;await detail.returnToLibrary();await tour.expectFailure();
 await scene(desktop,'recovery',async()=>{await pause(5500);await tour.retry();await tour.ready();await tour.expectReferences(24);});
 const mobile=await createCapture('mobile',375);await openApp(mobile);const small=new InspirationTourPage(mobile.page);
 await scene(mobile,'mobile',async()=>{await small.checkViewport();await pause(5000);await small.mobileScroll();await pause(3500);await small.checkViewport();});await closeCapture(mobile);
 await tour.comparison(mock,current);await scene(desktop,'close',async()=>{});
 expect(library.updates).toHaveLength(1);expect(library.calls).toContain('saveLink');expect(library.importCalls.filter(c=>c.operation==='request')).toHaveLength(0);
 expect(photos.critiqueRequests).toHaveLength(0);expect(errors).toEqual([]);expect(unexpectedRequests).toEqual([]);
 await closeCapture(desktop);
 writeFileSync(path.join(output,dry?'preview.json':'recording.json'),JSON.stringify({timeline,recordings,errors,unexpectedRequests,critiqueRequests:0,importRequests:0,notesSaved:true,linkSaved:true},null,2));
 console.log('Verified browsing, metadata, link save, retry, mobile fit; no analysis or import requests.');
} finally {await browser.close();}
