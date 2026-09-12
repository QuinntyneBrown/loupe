import { items } from './search-data.js';
import { containDialogFocus } from './dialog-focus.js';

export function setupFilters(onChange) {
  const dialog = document.querySelector('#search-filters-dialog');
  const trigger = document.querySelector('#open-filters');
  const active = document.querySelector('#active-filters');
  const error = document.querySelector('#filter-error');
  const groups = {
    boards: ['Window light', 'Quiet architecture', 'Street at night', 'Portrait sittings', 'Colour studies'],
    tags: ['window light', 'soft light', 'negative space', 'high contrast', 'muted palette', 'backlight', 'blue hour', 'symmetry', 'motion blur', 'minimal', 'architecturalgeometryanduninterruptedshadowstudies'],
  };
  let selected = { boards: [], tags: [] };
  let draft;

  function selectionText(filters) {
    return `${filters.boards.length} board${filters.boards.length === 1 ? '' : 's'} · ${filters.tags.length} tag${filters.tags.length === 1 ? '' : 's'}`;
  }

  function renderActive() {
    active.replaceChildren();
    const hasFilters = selected.boards.length + selected.tags.length > 0;
    trigger.setAttribute('aria-pressed', String(hasFilters));
    trigger.textContent = hasFilters ? `Boards and tags · ${selectionText(selected)}` : 'Boards and tags';
    for (const group of ['boards', 'tags']) {
      for (const value of selected[group]) {
        const remove = document.createElement('button');
        remove.className = 'tag';
        remove.type = 'button';
        remove.textContent = `${value} ×`;
        remove.setAttribute('aria-label', `Remove ${group.slice(0, -1)} filter ${value}`);
        remove.addEventListener('click', () => {
          selected[group] = selected[group].filter(item => item !== value);
          renderActive();
          trigger.focus();
          onChange();
        });
        active.append(remove);
      }
    }
    if (hasFilters) {
      const clear = document.createElement('button');
      clear.className = 'secondary';
      clear.type = 'button';
      clear.textContent = 'Clear all filters';
      clear.addEventListener('click', () => {
        selected = { boards: [], tags: [] };
        renderActive();
        trigger.focus();
        onChange();
      });
      active.append(clear);
    }
  }

  function renderChoices() {
    error.hidden = true;
    for (const group of ['boards', 'tags']) {
      const container = document.querySelector(group === 'boards' ? '#board-choices' : '#tag-choices');
      container.replaceChildren();
      for (const value of new Set([...groups[group], ...draft[group]])) {
        const available = groups[group].includes(value);
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'tag';
        const count = items.filter(item => item[group].includes(value)).length;
        button.textContent = available ? `${value} · ${count}` : `${value} · unavailable`;
        button.setAttribute('aria-label', available ? value : `${value} · unavailable`);
        button.setAttribute('aria-pressed', String(draft[group].includes(value)));
        button.addEventListener('click', () => {
          if (!draft[group].includes(value) && draft[group].length >= 10) {
            error.textContent = `Choose up to 10 ${group}.`;
            error.hidden = false;
            return;
          }
          error.hidden = true;
          draft[group] = draft[group].includes(value) ? draft[group].filter(item => item !== value) : [...draft[group], value];
          button.setAttribute('aria-pressed', String(draft[group].includes(value)));
          document.querySelector('#filter-selection').textContent = selectionText(draft);
        });
        container.append(button);
      }
    }
    document.querySelector('#filter-selection').textContent = selectionText(draft);
  }

  trigger.addEventListener('click', () => {
    draft = structuredClone(selected);
    renderChoices();
    dialog.showModal();
    document.querySelector('#board-choices button').focus();
  });
  dialog.addEventListener('close', () => trigger.focus());
  containDialogFocus(dialog);
  for (const id of ['#close-filters', '#cancel-filters']) document.querySelector(id).addEventListener('click', () => dialog.close());
  document.querySelector('#clear-draft').addEventListener('click', () => {
    draft = { boards: [], tags: [] };
    renderChoices();
  });
  document.querySelector('#filter-form').addEventListener('submit', event => {
    event.preventDefault();
    selected = structuredClone(draft);
    renderActive();
    dialog.close();
    onChange();
  });
  return {
    get value() { return selected; },
    reset(unavailable = false) {
      selected = { boards: unavailable ? ['Unavailable board'] : [], tags: [] };
      renderActive();
    },
  };
}
