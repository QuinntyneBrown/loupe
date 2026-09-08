import { defineConfig } from '@playwright/test';

// Records the loupe demo video. Requires the shared demo stack from
// docs/demo/harness/setup.ps1 (Postgres, demo identity provider, the
// loupe-demo-api / loupe-demo-worker containers, and the Angular dev server on
// https://localhost:4200) to already be running — this config does not start
// them, since they are long-lived and shared with the loupe-api recording.
// Kept separate from ../playwright.config.js: different baseURL, pacing, and
// video policy than the ordinary (mocked) acceptance run.
export default defineConfig({
  testDir: '.',
  testMatch: 'loupe.demo.spec.js',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 8 * 60_000,
  expect: { timeout: 30_000 },
  outputDir: './test-results/loupe',
  use: {
    baseURL: 'https://localhost:4200',
    browserName: 'chromium',
    viewport: { width: 1280, height: 720 },
    video: { mode: 'on', size: { width: 1280, height: 720 } },
    launchOptions: { slowMo: 90 },
    actionTimeout: 30_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
  },
});
