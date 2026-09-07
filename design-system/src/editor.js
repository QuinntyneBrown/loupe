const dialog = document.querySelector('dialog');
const trigger = document.querySelector('#open-editor');
const title = document.querySelector('#example-title');
const error = document.querySelector('#title-error');
trigger.addEventListener('click', () => dialog.showModal());
document.querySelector('#cancel-editor').addEventListener('click', () => dialog.close());
dialog.addEventListener('close', () => trigger.focus());
dialog.addEventListener('keydown', event => {
  if (event.key !== 'Tab') return;
  const controls = [...dialog.querySelectorAll('input:not(:disabled), select, textarea, button')];
  const first = controls[0], last = controls.at(-1);
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault(); last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault(); first.focus();
  }
});
document.querySelector('#editor').addEventListener('submit', event => {
  event.preventDefault();
  error.hidden = Boolean(title.value.trim());
  title.setAttribute('aria-invalid', String(!error.hidden));
  if (!error.hidden) { title.focus(); return; }
  document.querySelector('[aria-label="Editor feedback"]').textContent = `Saved example: ${title.value.trim()}`;
  dialog.close();
});
