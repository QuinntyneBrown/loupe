import { defineConfig } from 'vite';
import breakpoints from './src/breakpoints.json' with { type: 'json' };

export default defineConfig({
  build: { rollupOptions: { input: { reference: 'index.html', search: 'search.html' } } },
  plugins: [{
    name: 'loupe-grid-breakpoints',
    transform(code, id) {
      if (!id.endsWith('/reference.css')) return;
      const rules = Object.values(breakpoints).map((width, index) =>
        `@media (min-width: ${width}px) { .image-grid { grid-template-columns: repeat(${index + 2}, minmax(0, 1fr)); } }`);
      return { code: `${code}\n${rules.join('\n')}`, map: null };
    },
  }],
});
