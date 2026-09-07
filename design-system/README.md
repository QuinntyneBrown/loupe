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
The first increment contains an accessible button specimen. Further examples and
the complete token reference remain tracked in `../tasks/todo.md`.
