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

## Rebuilding the tree

`TaxonomyTreeBuilder` (`apps/api/src/Taxonomy.Core`) turns rows read from the database, ordered by `id`, back into a tree.

Rows are stored in pre-order: each entry follows its parent and a subtree is one contiguous run. The builder keeps a stack of the currently open ancestors. For each row it pops until the top of the stack is the row's parent path, appends the row to that parent's children and pushes it.

**Complexity.** n rows, path length at most L (486 characters in this dataset).

- Every row is pushed once and popped at most once, so there are O(n) stack operations in total.
- Each operation compares or slices a single path, O(L).
- Time is **O(n·L)**. With L bounded that is linear, and the full 60,942-row tree builds in ~6–14 ms.
- Space is O(n) for the tree itself and O(depth) for the stack (the tree is 12 levels deep).

**Why not a map from path to node?** That approach is also O(n·L) and also the obvious one, but it is wrong for this data. 211 paths occur more than once, e.g. two `flower > coneflower` siblings where only the second has children. A path lookup cannot tell which one `flower > coneflower > rudbeckia` belongs to. Pre-order position can: the parent is the closest preceding entry with that path. ImageNet also repeats whole subtrees under different parents, so the ambiguity is not rare.

The first row becomes the root, so the same builder works for any subtree slice, including one with deeper levels filtered out.

## Tests

```bash
dotnet test --solution apps/api/Taxonomy.slnx
```

Integration tests start a throwaway Postgres through Testcontainers, so Docker must be running.
