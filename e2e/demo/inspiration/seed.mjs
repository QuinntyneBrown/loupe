// Seeds the demo account's library through the real API: image uploads (real
// libvips processing), manual tags, boards, and two linked photographers. It
// refuses to run against a library that already holds references, so it can
// never add to a database that was not created for this recording.
import { readFileSync } from 'node:fs';
import { ApiClient } from './api-client.mjs';

const api = process.env.API_URL || 'https://localhost:5011';
const origin = process.env.APP_ORIGIN || 'https://localhost:4210';
const email = process.env.DEMO_EMAIL || 'photographer@example.com';
const password = process.env.DEMO_PASSWORD || 'local acceptance password';
const library = JSON.parse(readFileSync(new URL('./library.json', import.meta.url), 'utf8'));
const images = JSON.parse(readFileSync(new URL('./images.json', import.meta.url), 'utf8'));

const client = new ApiClient(api, origin);
await client.signIn(email, password);
const existing = await client.get('/api/references?pageSize=1');
if (existing.libraryCount !== 0) throw new Error(`Refusing to seed: the account already holds ${existing.libraryCount} references. Use a fresh demo database.`);
if ((await client.get('/api/boards')).length !== 0) throw new Error('Refusing to seed: the account already has boards.');

const boards = new Map();
for (const name of library.boards) { const board = await client.post('/api/boards', { name }); boards.set(name, board.id); }

const photographers = new Map();
// One catalogue entry (liveUpload) is left for the recording to upload through the UI.
// Upload in reverse so the first catalogue entry is the newest and appears first in the collection.
const seeded = library.references.filter(item => item.seed !== library.liveUpload);
for (const item of [...seeded].reverse()) {
  const form = new FormData();
  form.append('image', new Blob([readFileSync(new URL(`./images/${item.seed}.jpg`, import.meta.url))], { type: 'image/jpeg' }), `${item.seed}.jpg`);
  form.append('title', item.title);
  form.append('sourceUrl', images.find(image => image.seed === item.seed).url);
  form.append('attribution', item.attribution);
  form.append('notes', item.notes);
  let reference = await client.post('/api/references/images', form, { 'Idempotency-Key': `demo-seed-${item.seed}` });
  reference = await client.put(`/api/references/${reference.id}/tags`, { revision: reference.revision, tags: item.tags.map(([name, category]) => ({ name, category })) });
  if (item.boards.length) reference = await client.put(`/api/references/${reference.id}/boards`, { revision: reference.revision, boardIds: item.boards.map(name => boards.get(name)) });
  if (item.photographer) {
    if (photographers.has(item.photographer)) reference = await client.put(`/api/references/${reference.id}/photographer`, { revision: reference.revision, photographerId: photographers.get(item.photographer) });
    else {
      const profile = library.photographers.find(p => p.name === item.photographer);
      reference = await client.post(`/api/references/${reference.id}/photographer`, { revision: reference.revision, name: profile.name, portfolioUrl: profile.portfolioUrl }, { 'Idempotency-Key': `demo-seed-photographer-${item.seed}` });
      photographers.set(item.photographer, reference.photographer.id);
    }
  }
  console.log('Seeded', reference.title, `(${reference.tags.length} tags, ${reference.boardIds.length} boards${reference.photographer ? ', ' + reference.photographer.name : ''})`);
}
const page = await client.get('/api/references?pageSize=24');
if (page.libraryCount !== seeded.length) throw new Error(`Expected ${seeded.length} references, found ${page.libraryCount}.`);
console.log(JSON.stringify({ references: page.libraryCount, boards: (await client.get('/api/boards')).map(b => `${b.name} (${b.referenceCount})`), photographers: [...photographers.keys()] }, null, 2));
