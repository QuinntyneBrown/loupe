#!/usr/bin/env node
// Generates docs/mocks/index.html from the mock pages. Never hand-edit index.html.
//   node docs/mocks/build-index.mjs
// Reads each page's <title>, <meta name="description">, and its data-state, data-dialog,
// data-step, data-toast, and data-open-in attributes. Fails if a page with states lacks
// "default", a dialog has no close control, a data-open points at a missing dialog or
// step, or a link between pages targets a state, dialog, step, or toast that does not exist.
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

const AREAS = [
  { title: 'My Work', blurb: 'Upload a photograph with a brief, get a grounded critique, compare attempts.', pages: ['my-work.html', 'critique.html', 'compare.html'] },
  { title: 'Inspiration', blurb: 'Save references from a link or an upload, review AI tags, keep them on boards.', pages: ['inspiration.html', 'reference.html'] },
  { title: 'Photographers', blurb: 'Bookmark portfolios, keep notes, and link references to the people who made them.', pages: ['photographers.html', 'photographer.html'] },
  { title: 'Search', blurb: 'Describe what you want in plain language and combine it with type, board, and tag filters.', pages: ['search.html'] },
  { title: 'Account', blurb: 'Sign in, and see what the AI integration is doing.', pages: ['sign-in.html', 'settings.html'] },
  { title: 'System', blurb: 'Pages for a dead link and a recoverable failure.', pages: ['not-found.html', 'error.html'] },
  { title: 'Reference', blurb: 'Every reusable piece, grouped by the Angular library that will own it.', pages: ['components.html'] },
];

const FLOWS = [
  { title: 'Save, tag, board, find', steps: [
    ['inspiration.html#dialog=save-reference:form', 'Save a reference'],
    ['inspiration.html#dialog=save-reference:preview', 'Choose boards'],
    ['reference.html?state=ai-suggested', 'Review the AI tags'],
    ['inspiration.html?state=board', 'Open the board'],
    ['search.html?state=results', 'Find it by meaning'],
  ] },
  { title: 'Upload, critique, compare', steps: [
    ['my-work.html#dialog=upload:form', 'Upload with a brief'],
    ['critique.html?state=analyzing', 'Wait for the analysis'],
    ['critique.html', 'Read the critique'],
    ['critique.html#dialog=compare-pick', 'Pick a later attempt'],
    ['compare.html', 'Compare the two'],
  ] },
];

const GRAMMAR = [
  ['?state=name', 'Show one state of the screen. Omit for default.'],
  ['#dialog=name', 'Open a dialog. Multi-step dialogs take name:step.'],
  ['?toast=name', 'Show a toast and keep it on screen.'],
  ['?demo=1', 'Demo mode: adds the banner and relabels every AI block on the page.'],
  ['?chrome=0', 'Hide the mock toolbar, for screenshots.'],
];

const OMITTED = [
  'Forgot password and account recovery. Sign-in covers the first version.',
  'Delete all data. The brief asks for per-item deletion; each item deletes from its own page.',
  'An edit-tags dialog. Tags edit inline as chips, and accepted AI suggestions land in the same chips.',
  'Masonry grids. Tiles are a uniform 4:5 crop; the detail view shows the full frame.',
  'A dark theme. The light gallery-wall palette is the only one.',
  'Social features, public profiles, sharing, payments, and full-site crawling, as the brief defers them.',
  'A deleted-board filter chip in search results (L2-019). The filters dialog already explains the board/photographer exclusion; the rarer stale-URL case is a build-time concern, not a screen of its own.',
  'A mid-comparison deletion state in Compare (L2-005). One attempt disappearing while the screen is open is a real-time edge case, not a distinct static illustration.',
];

const attrAll = (html, name) => [...html.matchAll(new RegExp(`${name}="([^"]*)"`, 'g'))].map((m) => m[1]);
const uniq = (list) => list.filter((v, i) => list.indexOf(v) === i);
const escape = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

function readPage(file) {
  const html = readFileSync(join(root, file), 'utf8');
  const title = (/<title>(.*?)<\/title>/.exec(html)?.[1] ?? file).replace(/\s*·\s*Loupe$/, '');
  const description = /<meta name="description" content="([^"]*)">/.exec(html)?.[1] ?? '';
  const states = uniq(attrAll(html, 'data-state').flatMap((v) => v.split(/\s+/)).filter(Boolean));
  const dialogs = [];
  for (const m of html.matchAll(/<dialog\b([^>]*)>([\s\S]*?)<\/dialog>/g)) {
    const name = /data-dialog="([^"]+)"/.exec(m[1])?.[1];
    if (!name) continue;
    const openIn = /data-open-in="([^"]+)"/.exec(m[1])?.[1] ?? null;
    const steps = uniq(attrAll(m[2], 'data-step'));
    dialogs.push({ name, steps, openIn, hasClose: /data-close/.test(m[2]) });
  }
  const toasts = [];
  for (const m of html.matchAll(/<[a-z]+\b[^>]*data-toast="([^"]+)"[^>]*>/g)) {
    toasts.push({ name: m[1], openIn: /data-open-in="([^"]+)"/.exec(m[0])?.[1] ?? null });
  }
  const opens = attrAll(html, 'data-open').concat(attrAll(html, 'data-toast-open').map((t) => `toast:${t}`));
  const links = [...html.matchAll(/href="([a-z-]+\.html)(\?[^"#]*)?(#[^"]*)?"/g)].map((m) => ({ page: m[1], query: m[2] ?? '', hash: m[3] ?? '' }));
  return { file, title, description, states, dialogs, toasts, opens, links, hasDemo: /data-demo-only/.test(html) };
}

const pages = new Map();
for (const area of AREAS) for (const file of area.pages) {
  if (!existsSync(join(root, file))) { console.error(`Missing page: ${file}`); process.exit(1); }
  pages.set(file, readPage(file));
}

/* ---- validation ---- */
const errors = [];
for (const p of pages.values()) {
  if (p.states.length && !p.states.includes('default')) errors.push(`${p.file}: has states but no "default"`);
  for (const d of p.dialogs) if (!d.hasClose) errors.push(`${p.file}: dialog "${d.name}" has no [data-close]`);
  for (const d of p.dialogs) if (d.openIn && !p.states.includes(d.openIn)) errors.push(`${p.file}: dialog "${d.name}" opens in unknown state "${d.openIn}"`);
  for (const t of p.toasts) if (t.openIn && !p.states.includes(t.openIn)) errors.push(`${p.file}: toast "${t.name}" opens in unknown state "${t.openIn}"`);
  for (const o of p.opens) {
    if (o.startsWith('toast:')) { if (!p.toasts.some((t) => t.name === o.slice(6))) errors.push(`${p.file}: data-toast-open="${o.slice(6)}" has no toast`); continue; }
    const [name, step] = o.split(':');
    const d = p.dialogs.find((x) => x.name === name);
    if (!d) errors.push(`${p.file}: data-open="${o}" has no dialog`);
    else if (step && !d.steps.includes(step)) errors.push(`${p.file}: data-open="${o}" has no step "${step}"`);
  }
  for (const l of p.links) {
    if (l.page === 'index.html') continue;
    const target = pages.get(l.page);
    if (!target) { errors.push(`${p.file}: link to unknown page ${l.page}`); continue; }
    const q = new URLSearchParams(l.query.replace(/^\?/, ''));
    const state = q.get('state');
    if (state && !target.states.includes(state)) errors.push(`${p.file}: link ${l.page}?state=${state} targets a missing state`);
    const toast = q.get('toast');
    if (toast && !target.toasts.some((t) => t.name === toast)) errors.push(`${p.file}: link ${l.page}?toast=${toast} targets a missing toast`);
    const dm = /^#dialog=([\w-]+)(?::([\w-]+))?/.exec(l.hash);
    if (dm) {
      const d = target.dialogs.find((x) => x.name === dm[1]);
      if (!d) errors.push(`${p.file}: link ${l.page}${l.hash} targets a missing dialog`);
      else if (dm[2] && !d.steps.includes(dm[2])) errors.push(`${p.file}: link ${l.page}${l.hash} targets a missing step`);
    }
  }
}
if (errors.length) { console.error(errors.join('\n')); process.exit(1); }

/* ---- render ---- */
const chip = (href, label) => `<a class="lp-chip" href="${escape(href)}">${escape(label)}</a>`;
const stateQuery = (state) => (state && state !== 'default' ? `?state=${state}` : '');

function renderScreen(p) {
  const rows = [];
  if (p.states.length) rows.push(['States', p.states.map((s) => chip(`${p.file}${stateQuery(s)}`, s)).join('')]);
  if (p.dialogs.length) rows.push(['Dialogs', p.dialogs.flatMap((d) => (d.steps.length ? d.steps.map((s) => chip(`${p.file}${stateQuery(d.openIn)}#dialog=${d.name}:${s}`, `${d.name}:${s}`)) : [chip(`${p.file}${stateQuery(d.openIn)}#dialog=${d.name}`, d.name)])).join('')]);
  if (p.toasts.length) rows.push(['Toasts', p.toasts.map((t) => chip(`${p.file}${stateQuery(t.openIn)}${t.openIn && t.openIn !== 'default' ? '&' : '?'}toast=${t.name}`, t.name)).join('')]);
  if (p.hasDemo) rows.push(['Demo', chip(`${p.file}?demo=1`, 'demo=1')]);
  return `<article class="idx-screen">
  <div class="idx-screen__head"><h3><a href="${p.file}">${escape(p.title)}</a></h3><span class="lp-mono lp-faint">${p.file}</span></div>
  <p class="lp-small lp-muted">${escape(p.description)}</p>
  ${rows.length ? `<dl class="idx-rows">${rows.map(([k, v]) => `<div><dt>${k}</dt><dd>${v}</dd></div>`).join('')}</dl>` : ''}
</article>`;
}

const counts = [...pages.values()].reduce((a, p) => ({
  screens: a.screens + 1,
  states: a.states + p.states.length,
  dialogs: a.dialogs + p.dialogs.reduce((n, d) => n + Math.max(1, d.steps.length), 0),
  toasts: a.toasts + p.toasts.length,
}), { screens: 0, states: 0, dialogs: 0, toasts: 0 });

const html = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Loupe mocks</title>
<meta name="description" content="Generated directory of every mock screen, state, dialog, and toast.">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Instrument+Sans:ital,wght@0,400..700;1,400..700&family=Spline+Sans+Mono:wght@400;500&display=swap" rel="stylesheet">
<link rel="stylesheet" href="assets/tokens.css">
<link rel="stylesheet" href="assets/mocks.css">
<link rel="stylesheet" href="assets/mock.css">
</head>
<body class="lp-shell">
<main id="main" class="lp-main">
  <div class="lp-page-header">
    <div class="lp-page-header__text">
      <span class="lp-wordmark" aria-label="Loupe">L<span class="lp-wordmark__o"></span>upe</span>
      <h1 class="lp-title">Screen mocks</h1>
      <p class="lp-page-header__sub">${counts.screens} screens · ${counts.states} states · ${counts.dialogs} dialog steps · ${counts.toasts} toasts. Generated by <code class="lp-mono">build-index.mjs</code>; do not edit by hand.</p>
    </div>
  </div>

  <div class="idx-intro">
    <p class="lp-muted">Every screen is one HTML file holding all of its states and dialogs. The toolbar at the bottom right of each page switches between them, or use the URL grammar below. Check each screen at 390, 820, and 1440 pixels wide: under 640 the navigation becomes a tab bar and dialogs become bottom sheets.</p>
  </div>

  <section class="idx-area" aria-labelledby="flows">
    <div class="idx-area__head"><h2 class="lp-heading" id="flows">Core flows</h2><p class="lp-small lp-muted">The two journeys the product exists for, step by step.</p></div>
    <div class="lp-stack">
      ${FLOWS.map((f) => `<div class="idx-flow"><span class="lp-subheading">${escape(f.title)}</span><ol>${f.steps.map(([href, label]) => `<li>${chip(href, label)}</li>`).join('')}</ol></div>`).join('\n      ')}
    </div>
  </section>

  ${AREAS.map((area) => `<section class="idx-area" aria-labelledby="area-${area.title.toLowerCase().replace(/\s+/g, '-')}">
    <div class="idx-area__head"><h2 class="lp-heading" id="area-${area.title.toLowerCase().replace(/\s+/g, '-')}">${escape(area.title)}</h2><p class="lp-small lp-muted">${escape(area.blurb)}</p></div>
    <div class="idx-screens">
      ${area.pages.map((f) => renderScreen(pages.get(f))).join('\n      ')}
    </div>
  </section>`).join('\n\n  ')}

  <section class="idx-area" aria-labelledby="grammar">
    <div class="idx-area__head"><h2 class="lp-heading" id="grammar">URL grammar</h2></div>
    <dl class="idx-grammar">${GRAMMAR.map(([k, v]) => `<dt>${escape(k)}</dt><dd class="lp-muted">${escape(v)}</dd>`).join('')}</dl>
  </section>

  <section class="idx-area" aria-labelledby="omitted">
    <div class="idx-area__head"><h2 class="lp-heading" id="omitted">Deliberately omitted</h2><p class="lp-small lp-muted">Not forgotten. Left out to keep each screen to one job.</p></div>
    <ul class="idx-list">${OMITTED.map((o) => `<li>${escape(o)}</li>`).join('')}</ul>
  </section>
</main>
</body>
</html>
`;

writeFileSync(join(root, 'index.html'), html);
console.log(`index.html: ${counts.screens} screens, ${counts.states} states, ${counts.dialogs} dialog steps, ${counts.toasts} toasts`);
