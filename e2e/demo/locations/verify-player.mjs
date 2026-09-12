import { chromium } from 'playwright';
import { expect } from '@playwright/test';
import { readFileSync,writeFileSync } from 'node:fs';
import { PlayerPage } from '../inspiration/player-page.js';
const output=new URL('../../../docs/demo/locations/',import.meta.url);
const chapters=JSON.parse(readFileSync(new URL('chapters.json',output)));
const browser=await chromium.launch();const errors=[];
try {
 const page=await browser.newPage({viewport:{width:1440,height:1100}});page.on('pageerror',e=>errors.push(e.message));
 await page.goto(new URL('index.html',output).href);const player=new PlayerPage(page);const metadata=await player.metadata();
 expect(metadata.width).toBe(1440);expect(metadata.height).toBe(1040);expect(Math.abs(metadata.duration-chapters.reduce((n,c)=>n+c.duration,0))).toBeLessThan(.3);
 await player.play();for(const chapter of chapters)await player.chapter(chapter);
 await player.chapter(chapters[3]);await page.screenshot({path:new URL('source/player.png',output).pathname.replace(/^\/([A-Z]:)/,'$1')});
 const captions=readFileSync(new URL('captions.vtt',output),'utf8');expect(captions.startsWith('WEBVTT')).toBe(true);expect((captions.match(/-->/g)||[]).length).toBeGreaterThan(30);expect(errors).toEqual([]);
 writeFileSync(new URL('source/playback-check.json',output),JSON.stringify({metadata,chaptersVerified:chapters.length,played:true,errors},null,2));console.log('Playback and all chapters verified:',metadata);
} finally {await browser.close();}
