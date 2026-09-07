import { copyFile } from 'node:fs/promises';

await copyFile(
  new URL('../../design-system/src/tokens.css', import.meta.url),
  new URL('../projects/loupe/src/tokens.css', import.meta.url),
);
