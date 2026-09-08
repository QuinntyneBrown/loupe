import { defineConfig } from '@playwright/test';

// Records the design-system demo video. Separate from ../../playwright.config.js
// so normal acceptance pacing, retries, and video policy stay untouched.
export default defineConfig({
  testDir: '.',
  testMatch: 'design-system.demo.spec.js',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 6 * 60_000,
  expect: { timeout: 30_000 },
  outputDir: './test-results',
  use: {
    baseURL: 'http://127.0.0.1:4187',
    browserName: 'chromium',
    viewport: { width: 1280, height: 720 },
    video: { mode: 'on', size: { width: 1280, height: 720 } },
    launchOptions: { slowMo: 80 },
    actionTimeout: 30_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'npm run build && npm run preview',
    cwd: '../..',
    url: 'http://127.0.0.1:4187',
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
