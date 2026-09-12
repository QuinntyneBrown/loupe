import { containDialogFocus } from './dialog-focus.js';

const dialog = document.querySelector('dialog');
const trigger = document.querySelector('#open-editor');
const title = document.querySelector('#example-title');
const error = document.querySelector('#title-error');
trigger.addEventListener('click', () => dialog.showModal());
document.querySelector('#cancel-editor').addEventListener('click', () => dialog.close());
dialog.addEventListener('close', () => trigger.focus());
containDialogFocus(dialog);
document.querySelector('#editor').addEventListener('submit', event => {
  event.preventDefault();
  error.hidden = Boolean(title.value.trim());
  title.setAttribute('aria-invalid', String(!error.hidden));
  if (!error.hidden) { title.focus(); return; }
  document.querySelector('[aria-label="Editor feedback"]').textContent = `Saved example: ${title.value.trim()}`;
  dialog.close();
});
