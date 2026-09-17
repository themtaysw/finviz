# ImageNet Taxonomy Explorer

## Prerequisites

- Docker
- .NET 10 SDK
- Node 22+ and pnpm

## Running locally

```bash
docker compose up -d --wait
dotnet run --project apps/api/src/Taxonomy.Api
pnpm --dir apps/web install
pnpm --dir apps/web dev
```

API: http://localhost:5080 · Web: http://localhost:5173 (proxies `/api` to the API)

Postgres is exposed on host port `5433` to avoid clashing with a local install. Override with `DB_PORT`.
