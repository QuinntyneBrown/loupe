import { items } from './search-data.js';
import { setupFilters } from './search-filters.js';

const query = document.querySelector('#search-query');
const results = document.querySelector('#search-results');
const feedback = document.querySelector('#search-feedback');
const detail = document.querySelector('#search-detail');
let submittedQuery = '';
let visibleCount = 4;
let requestId = 0;
let completeHeldRequest;
let failMore = false;
let matches = [];
const completeButton = document.querySelector('#complete-loading');
const filters = setupFilters(() => search());

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text) node.textContent = text;
  return node;
}

function openDetail(item, trigger) {
  document.querySelector('#detail-title').textContent = item.title;
  document.querySelector('#detail-notes').textContent = item.notes;
  detail.addEventListener('close', () => trigger.focus(), { once: true });
  detail.showModal();
}

function card(item) {
  const article = element('article', `search-card search-card--${item.type}`);
  article.setAttribute('aria-label', item.title);
  if (item.type === 'reference') {
    const art = element('div', item.preview ? 'search-art synthetic-image' : 'search-art search-fallback', item.preview ? '' : 'No preview available');
    art.setAttribute('role', 'img');
    art.setAttribute('aria-label', item.preview ? `Synthetic composition for ${item.title}` : `No preview available for ${item.title}`);
    article.append(art);
  } else {
    article.append(element('span', 'eyebrow', 'Photographer'));
  }
  const body = element('div', 'search-card-body');
  const heading = element('h2');
  const open = element('button', 'search-card-open', item.title);
  open.type = 'button';
  open.setAttribute('aria-label', `Open ${item.title}`);
  open.addEventListener('click', () => openDetail(item, open));
  heading.append(open);
  body.append(heading);
  if (item.author) body.append(element('p', 'caption', item.author));
  if (item.type === 'photographer') body.append(element('p', 'search-card-notes', item.notes));
  const source = element('a', 'search-source', item.type === 'reference' ? '↗' : new URL(item.source).host + new URL(item.source).pathname + ' ↗');
  source.href = item.source;
  source.target = '_blank';
  source.rel = 'noopener noreferrer';
  source.setAttribute('aria-label', `Open ${item.type === 'reference' ? 'source' : 'website'} for ${item.title}`);
  if (item.type === 'reference') article.append(source);
  else body.append(source);
  if (item.type === 'photographer') {
    const tags = element('div', 'search-tags');
    for (const tag of item.tags) tags.append(element('span', 'tag', tag));
    body.append(tags);
    const foot = element('div', 'search-card-foot');
    const thumbs = element('div', 'search-thumbs');
    for (let index = 0; index < Math.min(item.count, 3); index++) {
      const thumb = element('span', 'synthetic-image');
      thumb.setAttribute('aria-hidden', 'true');
      thumbs.append(thumb);
    }
    foot.append(thumbs, element('span', 'caption', `${item.count} references`));
    body.append(foot);
  }
  article.append(body);
  return article;
}

function findMatches() {
  const type = document.querySelector('[name="type"]:checked').value;
  const words = submittedQuery.toLocaleLowerCase().split(/\s+/).filter(Boolean);
  return items.filter(item => {
    const text = [item.title, item.notes, ...item.tags].join(' ').toLocaleLowerCase();
    return (type === 'all' || type === item.type)
      && words.every(word => text.includes(word))
      && (!filters.value.boards.length || filters.value.boards.some(board => item.boards.includes(board)))
      && filters.value.tags.every(tag => item.tags.includes(tag));
  });
}

function render() {
  results.replaceChildren();
  feedback.textContent = `${Math.min(visibleCount, matches.length)} of ${matches.length} results${submittedQuery ? ` for “${submittedQuery}”` : ''}.`;
  if (matches.length) {
    const grid = element('div', 'search-grid');
    grid.append(...matches.slice(0, visibleCount).map(card));
    results.append(grid);
    if (visibleCount < matches.length) {
      const more = element('button', 'secondary search-more', 'Load more');
      more.type = 'button';
      more.addEventListener('click', () => request({ append: true, focus: true }));
      results.append(more);
    }
  } else {
    const empty = element('div', 'search-empty');
    empty.append(element('h2', '', submittedQuery ? `Nothing matched “${submittedQuery}”.` : 'Nothing matched your filters.'), element('p', 'caption', 'Try another keyword or widen your filters.'));
    if (filters.value.boards.includes('Unavailable board')) {
      empty.append(element('p', '', 'A selected board is unavailable. Remove it explicitly to search the remaining library.'));
    } else if (filters.value.boards.length && document.querySelector('[name="type"]:checked').value === 'photographer') {
      empty.append(element('p', '', 'Boards hold references only. Remove the board filter or choose All or References.'));
    }
    const edit = element('button', 'secondary', 'Edit query');
    edit.type = 'button';
    edit.addEventListener('click', () => query.focus());
    empty.append(edit);
    results.append(empty);
  }
}

function search(options = {}) {
  submittedQuery = query.value.trim();
  visibleCount = 4;
  document.querySelector('#search-intro').hidden = true;
  document.querySelector('.search-examples').hidden = true;
  document.querySelector('#search-hero').classList.add('search-hero--top');
  request(options);
}

async function request({ append = false, fail = false, hold = false, focus = false } = {}) {
  const currentRequest = ++requestId;
  const focusOrigin = document.activeElement;
  completeHeldRequest?.();
  completeHeldRequest = undefined;
  completeButton.hidden = !hold;
  const previousCount = append ? visibleCount : 0;
  const shouldFail = fail || (append && failMore);
  if (append) failMore = false;
  else matches = findMatches();
  results.setAttribute('aria-busy', 'true');
  feedback.textContent = append ? 'Loading more results…' : 'Searching your synthetic library…';
  if (append) {
    results.querySelector('.search-request-error')?.remove();
    const more = results.querySelector('.search-more');
    if (more) more.disabled = true;
  } else {
    const skeletons = element('div', 'search-grid');
    skeletons.setAttribute('aria-hidden', 'true');
    for (let index = 0; index < 4; index++) skeletons.append(element('div', 'search-skeleton'));
    results.replaceChildren(skeletons);
  }
  await new Promise(resolve => {
    if (hold) completeHeldRequest = resolve;
    else setTimeout(resolve, 250);
  });
  if (currentRequest !== requestId) return;
  completeHeldRequest = undefined;
  completeButton.hidden = true;
  results.setAttribute('aria-busy', 'false');
  if (shouldFail) {
    if (!append) results.replaceChildren();
    else results.querySelector('.search-more')?.remove();
    const error = element('div', 'search-empty search-request-error');
    const alert = element('div');
    alert.setAttribute('role', 'alert');
    alert.append(element('h2', '', append ? "More results couldn't be loaded." : "Search isn't available right now."));
    error.append(alert, element('p', 'caption', append ? 'Your loaded results and search are preserved.' : 'Your library is fine; only this synthetic request failed.'));
    const retry = element('button', 'secondary', append ? 'Retry more results' : 'Try again');
    retry.type = 'button';
    retry.addEventListener('click', () => request({ append, focus: true }));
    error.append(retry);
    results.append(error);
    feedback.textContent = '';
    return;
  }
  if (append) visibleCount += 4;
  const restoreFocus = focus && (document.activeElement === focusOrigin || document.activeElement === document.body);
  render();
  if (restoreFocus) {
    const target = results.querySelectorAll('.search-card-open')[previousCount] ?? results.querySelector('h2');
    if (target) {
      if (target.tagName === 'H2') target.tabIndex = -1;
      target.focus();
    }
  }
}

document.querySelector('#example-state').addEventListener('change', event => {
  const state = event.target.value;
  ++requestId;
  completeHeldRequest?.();
  completeHeldRequest = undefined;
  completeButton.hidden = true;
  document.querySelector('[name="type"][value="all"]').checked = true;
  filters.reset(state === 'Unavailable filters');
  failMore = state === 'Pagination recovery';
  query.value = state === 'Initial' ? '' : state === 'Empty' ? 'neon rain' : 'window';
  if (state === 'Initial') {
    submittedQuery = '';
    document.querySelector('#search-intro').hidden = false;
    document.querySelector('.search-examples').hidden = false;
    document.querySelector('#search-hero').classList.remove('search-hero--top');
    results.replaceChildren();
    results.setAttribute('aria-busy', 'false');
    feedback.textContent = '';
  } else {
    search({ hold: state === 'Loading', fail: state === 'Failure' });
  }
});
completeButton.addEventListener('click', () => completeHeldRequest?.());
document.querySelector('#search-form').addEventListener('submit', event => { event.preventDefault(); search(); });
document.querySelectorAll('[name="type"]').forEach(input => input.addEventListener('change', () => search()));
document.querySelectorAll('[data-query]').forEach(button => button.addEventListener('click', () => { query.value = button.dataset.query; search(); }));
document.querySelector('#close-detail').addEventListener('click', () => detail.close());
