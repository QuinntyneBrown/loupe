import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  workers: 2,
  retries: 0,
  forbidOnly: true,
  use: { baseURL: 'http://127.0.0.1:4207', actionTimeout: 5000, trace: 'retain-on-failure' },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
  ],
  webServer: {
    command: 'npm run start:e2e',
    cwd: '../frontend',
    url: 'http://127.0.0.1:4207',
    reuseExistingServer: false,
    timeout: 120000,
  },
});
