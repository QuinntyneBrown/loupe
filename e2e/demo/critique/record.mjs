import { chromium } from 'playwright';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { expect } from '@playwright/test';
import { SignInPage } from '../../page-objects/sign-in-page.js';
import { ComparePage } from '../../page-objects/compare-page.js';
import { CritiqueTourPage } from './tour-page.js';
import { ReferenceMockPage } from './mock-page.js';
import { makeLibrary, firstTitle, secondTitle, rockStatement, horizonStatement } from './fixture.js';

const root=fileURLToPath(new URL('../../..',import.meta.url));
const output=path.join(root,'docs/demo/critique/source');
mkdirSync(output,{recursive:true});
const dry=process.argv.includes('--dry');
const scenes=JSON.parse(readFileSync(dry?new URL('./scenes.json',import.meta.url):path.join(output,'narration.json')));
const browser=await chromium.launch();
const library=makeLibrary();
const errors=[]; const unexpectedRequests=[]; const timeline=[]; const recordings=[];
const pause=ms=>new Promise(resolve=>setTimeout(resolve,dry?Math.min(ms,100):ms));
async function createCapture(name,width=1440) {
  const context=await browser.newContext({viewport:{width,height:900},baseURL:'http://127.0.0.1:4207',recordVideo:dry?undefined:{dir:output,size:{width,height:900}}});
  await context.route('https://picsum.photos/**',route=>route.fulfill({body:readFileSync(new URL('./reference-photo.jpg',import.meta.url)),contentType:'image/jpeg'}));
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
  await library.attach(page);
  await page.setContent('<body style="margin:0;background:#ff00ff"></body>');
  await pause(2000);
  await page.evaluate(()=>document.body.style.background='#ffffff');
  const anchor=Date.now();
  const video=page.video();
  return {name,context,page,anchor,video,width};
}
async function openApp(capture) {
  await capture.page.goto('http://127.0.0.1:4207/my-work/'+library.photos[0].id);
  await new SignInPage(capture.page).continue();
  await new CritiqueTourPage(capture.page).ready();
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
  const desktop=await createCapture('desktop');
  const page=desktop.page; const tour=new CritiqueTourPage(page);
  await new ReferenceMockPage(page).open();
  await scene(desktop,'reference',async()=>{});
  await openApp(desktop);
  await scene(desktop,'layout',async()=>{});
  await scene(desktop,'evidence',async()=>{
    await pause(1800); await tour.hoverEvidence(rockStatement); await tour.spotlightVisible();
    await pause(4300); await tour.hoverEvidence(horizonStatement); await tour.spotlightVisible();
    await pause(3500); await tour.leaveEvidence();
  });
  await scene(desktop,'keyboard',async()=>{
    await tour.clickEvidence(rockStatement); await tour.leaveEvidence(); await tour.spotlightVisible();
    await pause(4000); await tour.dismissEvidence(); await tour.expectNoEvidenceHighlight();
    await pause(1300); await tour.focusEvidenceWithKeyboard(horizonStatement); await tour.spotlightVisible();
    await pause(4000); await tour.dismissEvidence(); await tour.blurEvidence();
  });
  await tour.section('Technical'); await pause(900);
  await scene(desktop,'technical',async()=>{await pause(5000); await tour.hoverEvidence('The pale sky is much brighter than the rocks. Bracket a darker frame to compare highlight detail.'); await pause(3000); await tour.leaveEvidence();});
  await tour.section('Composition and story'); await pause(900);
  await scene(desktop,'composition',async()=>{});
  await tour.section('Top three improvements'); await pause(900);
  await scene(desktop,'priorities',async()=>{await pause(2500);await tour.hoverEvidence('The brightest area sits above the horizon.');await pause(3500);await tour.leaveEvidence();});
  await tour.section('Practice exercise'); await pause(900);
  await scene(desktop,'notes',async()=>{
    await pause(3500); await tour.section('Your notes'); await pause(1000);
    await tour.notes('Try a darker exposure and a higher viewpoint next time.',dry);
  });
  await tour.top(); await pause(1000);
  await scene(desktop,'regenerate',async()=>{
    await pause(1800); await tour.regenerateCritique(); await tour.expectRegenerationConfirmation();
    await pause(9000); await tour.cancelRegeneration(); await tour.expectRegenerationCancelled(); await tour.menuClosed();
  });
  await scene(desktop,'compare',async()=>{
    await pause(1000); await tour.followHeaderCompare(library.photos[0].id);
    const compare=new ComparePage(page);await compare.expectChoosing('second');
    await pause(2500);await compare.choose(secondTitle);
    await expect(compare.side('First attempt')).toContainText(firstTitle);
    await expect(compare.side('Second attempt')).toContainText(secondTitle);
  });
  const mobile=await createCapture('mobile',375); await openApp(mobile); const small=new CritiqueTourPage(mobile.page);
  await scene(mobile,'mobile',async()=>{
    await small.checkViewport(); await pause(3500); await small.evidenceOnMobile(); await pause(1500);await small.checkViewport();
  });
  await closeCapture(mobile);
  await page.goto('http://127.0.0.1:4207/my-work/'+library.photos[0].id);
  await new SignInPage(page).continue(); await tour.ready();
  await scene(desktop,'close',async()=>{});
  expect(library.critiqueRequests).toHaveLength(0);
  expect(errors).toEqual([]); expect(unexpectedRequests).toEqual([]);
  expect(library.photos[0].notes).toBe('Try a darker exposure and a higher viewpoint next time.');
  await closeCapture(desktop);
  writeFileSync(path.join(output,dry?'preview.json':'recording.json'),JSON.stringify({timeline,recordings,errors,unexpectedRequests,critiqueRequests:library.critiqueRequests.length,notesSaved:true},null,2));
  console.log('Recording verified. No AI calls; notes and interaction assertions passed.');
} finally { await browser.close(); }
