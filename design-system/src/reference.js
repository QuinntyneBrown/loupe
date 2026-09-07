import './token-catalog.js';
import './editor.js';
import './gallery.js';

document.querySelector('#primary').addEventListener('click', () => {
  document.querySelector('[aria-label="Button feedback"]').textContent = 'Primary button activated.';
});
