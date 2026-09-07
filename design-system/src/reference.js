import './token-catalog.js';
import './editor.js';

document.querySelector('#primary').addEventListener('click', () => {
  document.querySelector('[aria-label="Button feedback"]').textContent = 'Primary button activated.';
});
