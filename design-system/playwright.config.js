import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/specs',
  fullyParallel: true,
  workers: 2,
  forbidOnly: true,
  retries: 0,
  use: { baseURL: 'http://127.0.0.1:4187', trace: 'retain-on-failure', actionTimeout: 5000 },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: { command: 'npm run build && npm run preview', url: 'http://127.0.0.1:4187', reuseExistingServer: false },
});
