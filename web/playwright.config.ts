import { defineConfig } from '@playwright/test'

// The end-to-end tests drive the real stack in a real browser.
// Start the API on port 5080 first; the Vite dev server is started here if it is not already running.
export default defineConfig({
  testDir: './e2e',
  workers: 1, // the tests share one database, so they run one at a time
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    channel: 'msedge', // use the installed Microsoft Edge, so no browser download is needed
    viewport: { width: 1280, height: 800 },
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
  },
})
