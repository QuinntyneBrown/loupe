import source from './tokens.css?raw';

const categories = [
  ['Color', 'color-', 'Surface, text, image, or feedback color'],
  ['Spacing', 'space-', 'Distance between content and controls'],
  ['Typography', '(font-|text-|leading-|weight-|tracking-|measure|heading-)', 'Text family, scale, rhythm, or measure'],
  ['Radius', 'radius-', 'Corner shape'],
  ['Borders', '(hairline|stroke)', 'Boundary thickness'],
  ['Elevation', 'shadow-', 'Surface depth'],
  ['Motion', '(duration-|ease)', 'Transition timing'],
  ['Focus', '(ring-width|skip-hidden)', 'Keyboard focus and skip navigation'],
  ['Sizing', '.', 'Layout or control dimension'],
];
const catalog = document.querySelector('#token-catalog');
const definitions = [...source.matchAll(/(--lp-[\w-]+):\s*([^;]+);/g)];
const rendered = new Set();
for (const [category, pattern, role] of categories) {
  const section = document.createElement('section');
  const heading = document.createElement('h3');
  heading.id = `tokens-${category.toLowerCase()}`;
  heading.textContent = category;
  section.setAttribute('aria-labelledby', heading.id);
  const list = document.createElement('ul');
  list.className = 'token-list';
  for (const [, name, value] of definitions) {
    if (rendered.has(name) || !new RegExp(`^--lp-${pattern}`).test(name)) continue;
    rendered.add(name);
    const item = document.createElement('li');
    const label = document.createElement('code');
    label.textContent = name;
    const detail = document.createElement('p');
    detail.textContent = `${role}: ${value}`;
    const example = document.createElement('div');
    example.className = `token-example token-example-${category.toLowerCase()}`;
    example.textContent = category === 'Color' ? '' : 'Aa · Loupe';
    example.setAttribute('aria-hidden', 'true');
    example.style.setProperty('--lp-example', `var(${name})`);
    item.append(example, label, detail);
    list.append(item);
  }
  section.append(heading, list);
  catalog.append(section);
}
