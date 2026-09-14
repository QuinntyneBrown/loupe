// Seeds the demo account's Locations library through the real API: location
// records, image uploads (real libvips processing) and tags, then waits for the
// real search index worker to embed every location through the local model. It
// refuses to run against an account that already holds locations, so it can
// never add to a database that was not created for this recording.
import { readFileSync } from 'node:fs';
import { ApiClient } from '../inspiration/api-client.mjs';

const api = process.env.API_URL || 'https://localhost:5013';
const origin = process.env.APP_ORIGIN || 'https://localhost:4212';
const email = process.env.DEMO_EMAIL || 'photographer@example.com';
const password = process.env.DEMO_PASSWORD || 'local acceptance password';
const library = JSON.parse(readFileSync(new URL('./library.json', import.meta.url), 'utf8'));

const client = new ApiClient(api, origin);
await client.signIn(email, password);
const existing = await client.get('/api/locations');
if (existing.totalCount !== 0) throw new Error(`Refusing to seed: the account already holds ${existing.totalCount} locations. Use a fresh demo database.`);

// One catalogue entry (liveAdd) is left for the recording to add through the UI.
// Create in reverse so the first catalogue entry is the newest and appears first in the grid.
const seeded = library.locations.filter(item => item.key !== library.liveAdd);
const ids = new Map();
for (const item of [...seeded].reverse()) {
  let location = await client.post('/api/locations', {
    name: item.name, locality: item.locality, region: item.region, country: item.country, setting: item.setting,
    coordinates: item.coordinates, scoutingBrief: item.scoutingBrief, notes: item.notes,
    tags: item.tags.map(([name, category]) => ({ name, category })),
  }, { 'Idempotency-Key': `demo-seed-location-${item.key}` });
  for (const [index, seed] of item.images.entries()) {
    const form = new FormData();
    form.append('image', new Blob([readFileSync(new URL(`../inspiration/images/${seed}.jpg`, import.meta.url))], { type: 'image/jpeg' }), `${seed}.jpg`);
    location = await client.post(`/api/locations/${location.id}/images`, form, { 'Idempotency-Key': `demo-seed-image-${item.key}-${index}` });
  }
  ids.set(item.key, location.id);
  console.log('Seeded', location.name, `(${location.images.length} images, ${location.tags.length} tags, index ${location.indexStatus})`);
}

// The worker (with Embeddings__Endpoint set) embeds each location; the grid should open settled.
const deadline = Date.now() + 120000;
let pending;
do {
  pending = [];
  for (const [key, id] of ids) {
    const location = await client.get(`/api/locations/${id}`);
    if (location.indexStatus !== 'current') pending.push(`${key}:${location.indexStatus}`);
  }
  if (pending.length) await new Promise(resolve => setTimeout(resolve, 1000));
} while (pending.length && Date.now() < deadline);
if (pending.length) throw new Error(`Search index did not settle: ${pending.join(', ')}. Is the stack running with -Ollama?`);

const page = await client.get('/api/locations');
if (page.totalCount !== seeded.length) throw new Error(`Expected ${seeded.length} locations, found ${page.totalCount}.`);
console.log(JSON.stringify({ locations: page.totalCount, indexed: ids.size, names: page.items.map(item => `${item.name} (${item.imageCount})`) }, null, 2));
