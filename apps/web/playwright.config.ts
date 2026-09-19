import { defineConfig } from '@playwright/test'

const isCI = Boolean(process.env.CI)
const deployedUrl = process.env.E2E_BASE_URL

export default defineConfig({
  testDir: 'e2e',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  reporter: isCI ? 'github' : 'list',
  use: {
    baseURL: deployedUrl ?? 'http://localhost:4173',
    channel: 'chrome',
    trace: 'retain-on-failure',
  },
  webServer: deployedUrl
    ? undefined
    : [
        {
          command: 'dotnet run --project ../api/src/Taxonomy.Api',
          url: 'http://localhost:5080/api/health',
          reuseExistingServer: true,
          timeout: 120_000,
        },
        {
          command: 'pnpm build && pnpm preview --port 4173 --strictPort',
          url: 'http://localhost:4173',
          reuseExistingServer: !isCI,
        },
      ],
})
