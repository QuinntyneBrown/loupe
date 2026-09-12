import { defineConfig, devices } from '@playwright/test';

const port = process.env.LOUPE_DESIGN_PORT ?? '4187';
const externalServer = process.env.LOUPE_DESIGN_EXTERNAL_SERVER === '1';

export default defineConfig({
  testDir: './tests/specs',
  fullyParallel: true,
  workers: 2,
  forbidOnly: true,
  retries: 0,
  use: { baseURL: `http://127.0.0.1:${port}`, trace: 'retain-on-failure', actionTimeout: 5000 },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: externalServer ? undefined : {
    command: `npm run build && npm run preview -- --port ${port}`,
    url: `http://127.0.0.1:${port}`,
    reuseExistingServer: false,
  },
});
