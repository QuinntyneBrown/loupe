const frame = document.querySelector('#gallery-frame');
const selected = document.querySelector('#gallery-selected');
const feedback = document.querySelector('#gallery-feedback');
const thumbs = document.querySelector('#gallery-thumbs');
const radios = () => [...thumbs.querySelectorAll('input[type="radio"]')];
const current = () => radios().find(radio => radio.checked);

function show(index) {
  frame.setAttribute('aria-label', `Synthetic location frame ${index}`);
  frame.style.setProperty('--lp-study-offset', `${index * 12}% auto auto ${index * 8}%`);
  selected.textContent = `Image ${index} of ${radios().length}`;
}
thumbs.addEventListener('change', () => show(Number(current().value)));

document.querySelector('#set-cover').addEventListener('click', () => {
  const chosen = current();
  for (const radio of radios()) radio.setAttribute('aria-label', `Image ${radio.value}`);
  chosen.setAttribute('aria-label', `Image ${chosen.value}, cover`);
  feedback.textContent = `Image ${chosen.value} is now the cover.`;
});

for (const cite of document.querySelectorAll('.cite')) {
  cite.addEventListener('click', () => {
    const target = radios().find(radio => radio.value === cite.dataset.image);
    target.checked = true;
    show(Number(target.value));
    target.focus();
  });
}

const meaningPill = document.querySelector('#meaning-pill');
for (const mode of document.querySelectorAll('input[name="mode"]')) {
  mode.addEventListener('change', () => { meaningPill.hidden = mode.value !== 'meaning' || !mode.checked; });
}

for (const chip of document.querySelectorAll('[aria-label="Shoot type"] .tag')) {
  chip.addEventListener('click', () => chip.setAttribute('aria-pressed', String(chip.getAttribute('aria-pressed') !== 'true')));
}

document.querySelector('#retry-index').addEventListener('click', () => {
  const status = document.querySelector('#index-status');
  status.className = 'status status--analyzing';
  status.textContent = 'Updating search';
});
