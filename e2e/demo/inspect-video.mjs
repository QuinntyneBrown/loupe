import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const [, , videoPath, posterOutPath, ...seekArgs] = process.argv;
const seeks = seekArgs.map(Number);

const server = createServer((req, res) => {
  const buf = readFileSync(videoPath);
  res.writeHead(200, { 'Content-Type': 'video/webm', 'Content-Length': buf.length, 'Accept-Ranges': 'bytes' });
  res.end(buf);
});
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const port = server.address().port;

const browser = await chromium.launch();
const page = await browser.newPage();
await page.setContent(`<video id="v" src="http://127.0.0.1:${port}/"></video>`);
const video = page.locator('#v');
await page.evaluate(() => new Promise((resolve) => {
  const v = document.getElementById('v');
  if (v.readyState >= 1) resolve(); else v.addEventListener('loadedmetadata', () => resolve(), { once: true });
}));
const meta = await page.evaluate(() => {
  const v = document.getElementById('v');
  return { duration: v.duration, width: v.videoWidth, height: v.videoHeight };
});
console.log('duration_s=', meta.duration.toFixed(2), 'width=', meta.width, 'height=', meta.height);

for (const t of seeks) {
  await page.evaluate((time) => new Promise((resolve) => {
    const v = document.getElementById('v');
    v.currentTime = time;
    v.addEventListener('seeked', () => resolve(), { once: true });
  }), t);
  console.log(`seeked to ${t}s ok`);
}

if (posterOutPath) {
  const lastSeek = seeks.length ? seeks[seeks.length - 1] : meta.duration * 0.5;
  await page.evaluate((time) => new Promise((resolve) => {
    const v = document.getElementById('v');
    v.currentTime = time;
    v.addEventListener('seeked', () => resolve(), { once: true });
  }), lastSeek);
  await video.screenshot({ path: posterOutPath });
  console.log('poster saved to', posterOutPath);
}

await browser.close();
server.close();
