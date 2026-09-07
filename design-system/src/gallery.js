const grid = document.querySelector('#image-grid');
for (let index = 1; index <= 10; index++) {
  const item = document.createElement('li');
  const art = document.createElement('div');
  art.className = 'synthetic-image';
  art.setAttribute('role', 'img');
  art.setAttribute('aria-label', `Synthetic study ${index}: a neutral frame with a blue circle`);
  const caption = document.createElement('p');
  caption.textContent = `Study ${String(index).padStart(2, '0')} · Synthetic`;
  item.append(art, caption);
  grid.append(item);
}
document.querySelector('#tag-example').addEventListener('click', event => {
  const button = event.currentTarget;
  button.setAttribute('aria-pressed', String(button.getAttribute('aria-pressed') !== 'true'));
});
document.querySelector('#retry-example').addEventListener('click', () => {
  document.querySelector('[aria-label="Recovery feedback"]').textContent = 'Example loaded. Your draft is preserved.';
});
