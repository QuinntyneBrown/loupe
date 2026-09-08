/* Mock-only behaviour. Nothing here ships.
   URL grammar:  ?state=<name>  ?toast=<name>  ?demo=1  ?chrome=0  #dialog=<name>[:<step>]
   Markup contract:
     [data-state="a b"]        block visible only in the listed states (absent = always)
     dialog[data-dialog="x"]   one native dialog per flow; [data-step] sections inside it
     [data-open="x[:step]"]    opens a dialog on click       [data-close] closes its dialog
     [data-toast="x"]          toast; [data-toast-open="x"] shows it on click; [data-toast-close] hides
     [data-demo-only] / [data-live-only]   visible only with / without ?demo=1
     [data-open-in="state"]  on a dialog or toast: the state build-index.mjs links it from (default otherwise)
     [data-save-editor]        wraps one explicit-Save editor (L2-004, L2-043): typing in its
       [data-save-input] marks [data-save-status] Unsaved and enables [data-save-button];
       clicking that button runs Saving, then Saved, and disables the button again */
(function () {
  'use strict';
  var params = new URLSearchParams(location.search);
  var body = document.body;
  var demo = params.get('demo') === '1';
  var chrome = params.get('chrome') !== '0';
  if (demo) body.setAttribute('data-demo', '');

  /* ---- states ---- */
  var stateEls = toArray(document.querySelectorAll('[data-state]'));
  var states = [];
  stateEls.forEach(function (el) {
    tokens(el.getAttribute('data-state')).forEach(function (s) {
      if (states.indexOf(s) < 0) states.push(s);
    });
  });
  var state = params.get('state') || 'default';
  if (states.length && states.indexOf(state) < 0) state = 'default';
  body.setAttribute('data-active-state', state);

  function applyVisibility() {
    stateEls.forEach(function (el) {
      el.hidden = tokens(el.getAttribute('data-state')).indexOf(state) < 0;
    });
    toArray(document.querySelectorAll('[data-demo-only]')).forEach(function (el) {
      if (!demo) el.hidden = true;
      else if (!el.hasAttribute('data-state')) el.hidden = false;
    });
    toArray(document.querySelectorAll('[data-live-only]')).forEach(function (el) {
      if (demo) el.hidden = true;
      else if (!el.hasAttribute('data-state')) el.hidden = false;
    });
  }
  applyVisibility();

  /* ---- dialogs ---- */
  var dialogs = toArray(document.querySelectorAll('dialog[data-dialog]'));
  function findDialog(name) {
    for (var i = 0; i < dialogs.length; i++) if (dialogs[i].getAttribute('data-dialog') === name) return dialogs[i];
    return null;
  }
  function stepsOf(d) { return toArray(d.querySelectorAll('[data-step]')); }
  function openDialog(name, step) {
    var d = findDialog(name);
    if (!d) return false;
    var steps = stepsOf(d);
    if (steps.length) {
      var names = steps.map(function (s) { return s.getAttribute('data-step'); });
      var target = step && names.indexOf(step) >= 0 ? step : names[0];
      steps.forEach(function (s) { s.hidden = s.getAttribute('data-step') !== target; });
      d.setAttribute('data-active-step', target);
      step = target;
    }
    dialogs.forEach(function (o) { if (o !== d && o.open) o.close(); });
    if (!d.open) d.showModal();
    var hash = '#dialog=' + name + (step ? ':' + step : '');
    if (location.hash !== hash) history.replaceState(null, '', location.pathname + location.search + hash);
    syncBar();
    return true;
  }
  dialogs.forEach(function (d) {
    d.addEventListener('close', function () {
      if (location.hash.indexOf('#dialog=') === 0) history.replaceState(null, '', location.pathname + location.search);
      syncBar();
    });
    d.addEventListener('click', function (e) { if (e.target === d) d.close(); });
  });
  function openFromHash() {
    var m = /^#dialog=([\w-]+)(?::([\w-]+))?/.exec(location.hash);
    if (m) openDialog(m[1], m[2]);
  }
  window.addEventListener('hashchange', openFromHash);

  /* ---- toasts ---- */
  var toasts = toArray(document.querySelectorAll('[data-toast]'));
  var toastTimer = 0;
  function showToast(name, sticky) {
    toasts.forEach(function (t) { t.hidden = t.getAttribute('data-toast') !== name; });
    clearTimeout(toastTimer);
    if (!sticky) toastTimer = setTimeout(hideToasts, 5000);
  }
  function hideToasts() { toasts.forEach(function (t) { t.hidden = true; }); }
  toasts.forEach(function (t) { t.hidden = true; });
  if (params.get('toast')) showToast(params.get('toast'), true);

  /* ---- delegated clicks ---- */
  document.addEventListener('click', function (e) {
    var t = e.target && e.target.closest ? e.target : null;
    if (!t) return;
    var opener = t.closest('[data-open]');
    if (opener) { e.preventDefault(); var parts = opener.getAttribute('data-open').split(':'); openDialog(parts[0], parts[1]); return; }
    var closer = t.closest('[data-close]');
    if (closer) { e.preventDefault(); var d = closer.closest('dialog'); if (d) d.close(); return; }
    var toastOpener = t.closest('[data-toast-open]');
    if (toastOpener) { e.preventDefault(); var dl = toastOpener.closest('dialog'); if (dl) dl.close(); showToast(toastOpener.getAttribute('data-toast-open')); return; }
    var toastCloser = t.closest('[data-toast-close]');
    if (toastCloser) { e.preventDefault(); hideToasts(); }
    var saveButton = t.closest('[data-save-button]');
    if (saveButton && !saveButton.disabled) {
      var editor = saveButton.closest('[data-save-editor]');
      setSaveStatus(editor, 'saving');
      saveButton.disabled = true;
      setTimeout(function () { setSaveStatus(editor, 'saved'); }, 700);
    }
  });

  /* ---- explicit-Save editors (L2-004, L2-043) ---- */
  var SAVE_LABELS = { unsaved: 'Unsaved changes', saving: 'Saving', saved: 'Saved', failed: "Couldn't save" };
  function setSaveStatus(editor, kind) {
    if (!editor) return;
    var status = editor.querySelector('[data-save-status]');
    if (!status) return;
    status.className = 'lp-save-status lp-save-status--' + kind;
    status.textContent = SAVE_LABELS[kind] || kind;
  }
  toArray(document.querySelectorAll('[data-save-editor]')).forEach(function (editor) {
    var input = editor.querySelector('[data-save-input]');
    var button = editor.querySelector('[data-save-button]');
    if (!input || !button) return;
    input.addEventListener('input', function () {
      if (button.disabled) { button.disabled = false; setSaveStatus(editor, 'unsaved'); }
    });
  });

  /* ---- toolbar ---- */
  var bar = null, barSummary = null;
  function link(href, label, current) {
    var a = document.createElement('a');
    a.href = href; a.textContent = label;
    if (current) a.setAttribute('aria-current', 'true');
    return a;
  }
  function withParams(changes) {
    var p = new URLSearchParams(location.search);
    Object.keys(changes).forEach(function (k) { if (changes[k] === null) p.delete(k); else p.set(k, changes[k]); });
    var q = p.toString();
    return location.pathname + (q ? '?' + q : '');
  }
  function group(label) {
    var g = document.createElement('div'); g.className = 'mock-bar__group';
    var l = document.createElement('div'); l.className = 'mock-bar__label'; l.textContent = label; g.appendChild(l);
    return g;
  }
  function buildBar() {
    if (!chrome) return;
    bar = document.createElement('details'); bar.className = 'mock-bar';
    barSummary = document.createElement('summary'); bar.appendChild(barSummary);
    var panel = document.createElement('div'); panel.className = 'mock-bar__panel'; bar.appendChild(panel);
    var top = document.createElement('div'); top.className = 'mock-bar__row';
    top.appendChild(link('index.html', 'Index'));
    top.appendChild(link(withParams({ demo: demo ? null : '1', toast: null }), demo ? 'Demo: on' : 'Demo: off', demo));
    panel.appendChild(top);
    if (states.length) {
      var gs = group('State');
      states.forEach(function (s) { gs.appendChild(link(withParams({ state: s === 'default' ? null : s, toast: null }), s, s === state)); });
      panel.appendChild(gs);
    }
    if (dialogs.length) {
      var gd = group('Dialog');
      dialogs.forEach(function (d) {
        var name = d.getAttribute('data-dialog');
        var steps = stepsOf(d);
        if (steps.length) steps.forEach(function (s) {
          var st = s.getAttribute('data-step');
          gd.appendChild(link('#dialog=' + name + ':' + st, name + ' : ' + st));
        });
        else gd.appendChild(link('#dialog=' + name, name));
      });
      panel.appendChild(gd);
    }
    if (toasts.length) {
      var gt = group('Toast');
      toasts.forEach(function (t) { var n = t.getAttribute('data-toast'); gt.appendChild(link(withParams({ toast: n }), n, params.get('toast') === n)); });
      panel.appendChild(gt);
    }
    document.body.appendChild(bar);
    syncBar();
  }
  function syncBar() {
    if (!barSummary) return;
    var open = null;
    dialogs.forEach(function (d) { if (d.open) open = d.getAttribute('data-dialog') + (d.getAttribute('data-active-step') ? ':' + d.getAttribute('data-active-step') : ''); });
    barSummary.textContent = state + (open ? ' · ' + open : '') + (demo ? ' · demo' : '');
  }

  function toArray(list) { return Array.prototype.slice.call(list); }
  function tokens(s) { return (s || '').split(/\s+/).filter(Boolean); }

  buildBar();
  openFromHash();
})();
