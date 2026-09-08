import { copyFile, cp } from 'node:fs/promises';

await copyFile(
  new URL('../../design-system/src/tokens.css', import.meta.url),
  new URL('../projects/loupe/src/tokens.css', import.meta.url),
);

// tokens.css references self-hosted font files at /fonts/*; mirror the same
// vendored files design-system serves so both projects resolve them identically.
await cp(
  new URL('../../design-system/public/fonts', import.meta.url),
  new URL('../projects/loupe/public/fonts', import.meta.url),
  { recursive: true },
);
