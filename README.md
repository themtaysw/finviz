# ImageNet Taxonomy Explorer

## Prerequisites

- Docker
- .NET 10 SDK
- Node 22+ and pnpm

## Running locally

```bash
docker compose up -d --wait
dotnet run --project apps/api/src/Taxonomy.Ingest -- load data/structure_released.xml
dotnet run --project apps/api/src/Taxonomy.Api
pnpm --dir apps/web install
pnpm --dir apps/web dev
```

API: http://localhost:5080 · Web: http://localhost:5173 (proxies `/api` to the API)

Postgres is exposed on host port `5433` to avoid clashing with a local install. Override with `DB_PORT`.

## Data

`data/structure_released.xml` is vendored from [tzutalin/ImageNet_Utils](https://github.com/tzutalin/ImageNet_Utils/blob/master/detection_eval_tools/structure_released.xml) so the build is reproducible offline.

Export it to the linear `(name, size)` form:

```bash
dotnet run --project apps/api/src/Taxonomy.Ingest -- export data/structure_released.xml linear.json
```

`load` applies pending migrations and then replaces the stored taxonomy in a single transaction, so it is safe to re-run.

## Tests

```bash
dotnet test --solution apps/api/Taxonomy.slnx
```

Integration tests start a throwaway Postgres through Testcontainers, so Docker must be running.
