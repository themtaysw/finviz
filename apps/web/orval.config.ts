import { defineConfig } from 'orval'

export default defineConfig({
  taxonomy: {
    input: '../api/openapi/taxonomy.json',
    output: {
      target: 'src/api/generated/taxonomy.ts',
      schemas: 'src/api/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        mutator: { path: 'src/api/fetcher.ts', name: 'fetcher' },
        fetch: { includeHttpResponseReturnType: false },
        query: { signal: true },
      },
    },
  },
})
