import { defineConfig } from '@playwright/test';

// Records the loupe-api demo video. Requires the shared demo stack from
// docs/demo/harness/setup.ps1 (Postgres, local accounts, the
// loupe-demo-api / loupe-demo-worker containers) to already be running.
// Kept separate from ../playwright.config.js: different pacing, video
// policy, and webServer than the ordinary acceptance run.
export default defineConfig({
  testDir: '.',
  testMatch: 'loupe-api.demo.spec.js',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 6 * 60_000,
  expect: { timeout: 30_000 },
  outputDir: './test-results/loupe-api',
  use: {
    baseURL: 'http://127.0.0.1:4302',
    browserName: 'chromium',
    viewport: { width: 1280, height: 720 },
    video: { mode: 'on', size: { width: 1280, height: 720 } },
    launchOptions: { slowMo: 60 },
    actionTimeout: 30_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'node loupe-api-terminal-server.mjs',
    cwd: '.',
    url: 'http://127.0.0.1:4302',
    reuseExistingServer: false,
    timeout: 30_000,
  },
});
