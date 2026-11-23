import { defineConfig } from '@playwright/test'

const baseURL = process.env.TASKPILOT_E2E_URL
if (!baseURL) throw new Error('Defina TASKPILOT_E2E_URL para uma instância de teste dedicada.')

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  timeout: 90_000,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL,
    browserName: 'chromium',
  },
})
