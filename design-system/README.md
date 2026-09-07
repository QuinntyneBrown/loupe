# Loupe design system

Requires Node 24.18.0. This static site works with the application and API stopped.

```powershell
cd design-system
npm ci
npx playwright install chromium firefox webkit
npm test
npm run build
npm run preview
```

Publish the contents of `dist/` to any static host. No credentials or runtime API
are needed. `src/tokens.css` owns Loupe's visual tokens, seeded from the mockups.
The reference includes buttons, forms, modal editing, selected tags, status pills,
synthetic image grids, progress, and empty/error examples. Preview uses port 4187.
The build reads `src/breakpoints.json` to generate the specified grid rules.
Acceptance progress and remaining manual release checks are in `../tasks/todo.md`.
