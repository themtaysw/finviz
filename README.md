# ImageNet Taxonomy Explorer

Browse and search the 60,942 categories of the ImageNet 2011 taxonomy. The XML is flattened into `(path, size)` rows, stored in Postgres, rebuilt into a tree on demand, and shown in a lazily loaded, virtualized tree with search.

**Live:** https://app-production-ab35.up.railway.app

Stack: .NET 10 minimal API with Dapper and Npgsql, Postgres (17 locally, 18 on Railway), React 19 with TypeScript, TanStack Query and TanStack Virtual, Vite. Docker for the local stack, Railway for the deployment.

## Running it

```bash
docker compose up --build
```

Then open http://localhost:8080. Compose starts Postgres, runs the ingest once (migrations plus loading the XML, about a second) and starts the app only after that succeeded.

### Developing

Prerequisites: Docker, .NET 10 SDK, Node 22+ with pnpm.

```bash
docker compose up -d --wait db
dotnet run --project apps/api/src/Taxonomy.Ingest.Cli -- load data/structure_released.xml
dotnet run --project apps/api/src/Taxonomy.Api
pnpm --dir apps/web install
pnpm --dir apps/web dev
```

API on http://localhost:5080, web with hot reload on http://localhost:5173 (it proxies `/api`). Postgres is on host port `5433` so it doesn't clash with a local install; override with `DB_PORT`.

## Design decisions

**The row id is the entry's position in pre-order.** The parser emits entries in document order, where every subtree is one contiguous run. With `id` as that position, the descendants of a node are exactly the ids `id + 1 … id + size`. Children are the rows one level deeper in that range, so listing them is a range scan on a `(depth, id)` index instead of a recursive query: about 0.1 ms in Postgres even for the root. This is the classic nested-set layout, except the data hands it to us for free.

**Paths are not unique, so nothing is looked up by path.** I profiled the XML before writing code: 211 paths occur more than once (two `flower > coneflower` siblings, only one of which has children), and ImageNet repeats whole subtrees under different parents. Every place that needs a parent resolves it by position: the ingest, the tree rebuild and the UI.

**The client knows the tree's shape before it loads it.** Each node carries `childCount`, and the client only remembers which nodes are expanded and where they sit among their siblings. From that it computes every visible row up front. Content arrives in pages of 100, only for rows near the viewport, so the list is virtualized from the start and a 2,350-child node costs the same as a 3-child one.

**Cancellation goes all the way down.** Every fetch carries an abort signal, every endpoint passes the request's token to Npgsql, and Postgres cancels the query. Scrolling past a page or typing past a search aborts the stale request in the browser and stops its SQL on the server.

**The API contract is generated, not duplicated.** The OpenAPI document is written on every build and committed. The web client's types and TanStack Query hooks are generated from it with Orval, and `check:api` fails if they drift.

**One deployable image.** The API serves the built React app, so there's one origin, no proxy and no CORS. The same image carries the ingest CLI and the XML, which the deploy runs before switching traffic.

## What I focused on most

Request cancellation, render performance and correctness on the real data, roughly in that order.

For cancellation I wanted proof, not a `CancellationToken` parameter that nobody checks. There are tests at every layer: an API test that holds a table lock, drops the request and asks `pg_stat_activity` whether the query is gone. A component test checks that scrolling past a slow page aborts it. A search test checks that a newer query aborts the older one. An end-to-end test watches Chrome report `ERR_ABORTED`. Each of them fails if the signal is dropped. I checked that by breaking the code on purpose.

For rendering, the DOM holds about 45 rows regardless of how much is expanded, rows re-render only when their own data changes, and a deep link into the largest node (2,350 children) fetches 3 pages instead of 24.

## What I'm most proud of

The cancellation test that almost lied. My first version passed even after I made the repository ignore the token. Dumping `pg_stat_activity` showed the query was cancelled anyway, and running the same broken code behind a real Kestrel server showed it wasn't. The in-memory test server tears down in-flight work when a client disconnects, which hides whether the application does. The test now runs against a real socket and fails with "Expected 0 running API queries, found 1" when the token is ignored.

Also the tree needing no data to know its shape. Deep links scroll to the right row before that row's page has loaded, and the virtualizer can size a 66,000-pixel list from counts alone.

## Trade-offs

- **Dapper and hand-written SQL instead of EF Core.** The queries are few and Postgres-specific (binary `COPY`, trigram search, range scans over the id layout). SQL keeps them readable and fast. The cost is manual mapping and no LINQ.
- **A small migrator (about 100 lines) instead of DbUp or EF migrations.** It applies embedded scripts once, serialized by a Postgres advisory lock so overlapping deploys can't race. There are no down-migrations.
- **Offset paging instead of keyset.** Virtualization needs random access to any page, and the longest child list is 2,350 rows, where `OFFSET` is cheap. With millions of children I'd switch to keyset paging plus an anchor.
- **Replacing the whole dataset on load.** `TRUNCATE` and `COPY` in one transaction is simple and atomic: readers see the old data or the new, never half of it. It holds an exclusive lock for the duration of the load (one to three seconds), during which reads wait. For a larger dataset I'd load into a shadow table and swap.
- **Substring search with a trigram index, no typo tolerance.** `ILIKE` matches what people expect when they type part of a word. `pg_trgm` similarity could add fuzzy matching later.
- **The path is stored as the assignment's linear form.** That repeats every ancestor's name in every row (about 10 MB of text), so `label`, `parent_id`, `depth` and `child_count` are derived once at load time and queries never parse paths.
- **Enter selects; arrow keys only move the focus.** "Selection follows focus" feels snappier, but every keypress would load a node and push a history entry.
- **Deployment settings live in Railway, not the repo.** Railway's `railway.json` is deprecated for new services, and its replacement would add an npm package to a .NET/React repo for a single service. The settings are listed below instead.

What I'd do next: a CI pipeline running the three test suites plus `check:api`, HTTP caching with ETags (the data only changes on deploy), and a treemap of the selected node built on the `/tree` endpoint, which is implemented and tested but not yet used by the UI.

## Data and ingest

`data/structure_released.xml` is vendored from [tzutalin/ImageNet_Utils](https://github.com/tzutalin/ImageNet_Utils/blob/master/detection_eval_tools/structure_released.xml) so the build is reproducible offline.

The parser streams the XML in a single pass. A synset's size (its number of descendants) is only known at its closing tag, so its entry is filled in then. DTDs are rejected, which rules out XXE, and invalid labels fail with a line number.

```bash
dotnet run --project apps/api/src/Taxonomy.Ingest.Cli -- export data/structure_released.xml linear.json
dotnet run --project apps/api/src/Taxonomy.Ingest.Cli -- load data/structure_released.xml
```

`export` writes the linear form as JSON. `load` applies pending migrations and replaces the stored taxonomy in one transaction, so it is safe to re-run.

## Rebuilding the tree

`TaxonomyTreeBuilder` (`apps/api/src/Taxonomy.Core`) turns rows read from the database, ordered by `id`, back into a tree. It keeps a stack of the currently open ancestors. For each row it pops until the top of the stack is the row's parent path, appends the row to that parent's children and pushes it.

**Complexity.** n rows, path length at most L (486 characters here).

- Every row is pushed once and popped at most once: O(n) stack operations.
- Each operation compares or slices one path: O(L).
- Time is **O(n·L)**, linear for bounded paths. The full 60,942-row tree builds in about 6–14 ms.
- Space is O(n) for the tree and O(depth) for the stack (the tree is 12 levels deep).

**Why not a map from path to node?** It is the obvious approach and also O(n·L), but it is wrong for this data. With two `flower > coneflower` siblings, a path lookup can't tell which one `flower > coneflower > rudbeckia` belongs to. Pre-order position can: the parent is the closest preceding entry with that path. Swapping the stack for a path map makes three builder tests fail, including the round trip over the real file.

The first row becomes the root, so the same builder works for any subtree slice, including one with deeper levels filtered out.

## API

| Endpoint                                          | Purpose                                                                                 |
| ------------------------------------------------- | --------------------------------------------------------------------------------------- |
| `GET /api/nodes/roots`                            | Top-level nodes                                                                         |
| `GET /api/nodes/{id}`                             | A node with its path and ancestors (with their positions, for expanding to a deep link) |
| `GET /api/nodes/{id}/children?offset&limit&order` | One page of children, ordered by `source`, `name` or `size`                             |
| `GET /api/nodes/{id}/tree?depth`                  | The subtree as nested nodes, built by `TaxonomyTreeBuilder`                             |
| `GET /api/search?q&limit`                         | Label search                                                                            |
| `GET /api/health`                                 | Readiness, including a database round trip                                              |

Typical responses take 2–4 ms on the server. The worst case measured is a one-character search matching 44,000 rows at about 45 ms, which the UI never sends (it searches from two characters).

Search treats each comma-separated synonym as a name. Exact synonym matches rank first, then synonyms that start with the query, then larger subtrees. My first ranking (shortest label first) put "dog" meaning a person ahead of the animal, whose label is `dog, domestic dog, Canis familiaris`.

## Frontend

- **Tree:** virtualized, paged by viewport, with the WAI-ARIA tree keyboard pattern (arrows, Home/End, Page Up/Down, Left/Right, Enter). Focus stays on the tree through `aria-activedescendant`, because virtualization can unmount a focused row mid-scroll.
- **Search:** an ARIA combobox, debounced at 200 ms, with the stale request aborted. The previous results stay on screen, dimmed, until new ones arrive, so the list doesn't flicker. Each result shows its ancestors, which is what tells the four `dog, domestic dog` entries apart.
- **Details:** breadcrumbs, synonyms, descendant counts, and the largest subcategories as shares of the subtree.
- **URL:** the selection is kept in the URL (`?node=123`), so deep links and Back/Forward work.

## Tests

| Suite                                               | Count | Command                                         |
| --------------------------------------------------- | ----- | ----------------------------------------------- |
| .NET unit                                           | 32    | `dotnet test --solution apps/api/Taxonomy.slnx` |
| .NET integration (real Postgres via Testcontainers) | 34    | same as above; needs Docker                     |
| Web components (jsdom + MSW)                        | 40    | `pnpm --dir apps/web test`                      |
| End-to-end (Chrome, real stack)                     | 5     | `pnpm --dir apps/web e2e`                       |

The end-to-end suite builds the production bundle and starts the API if nothing is running on port 5080. `E2E_BASE_URL=<url>` runs it against a deployment instead.

## Deployment

Railway runs one service built from the root `Dockerfile`, plus a managed Postgres:

| Setting                       | Value                                                                           |
| ----------------------------- | ------------------------------------------------------------------------------- |
| Pre-deploy command            | `dotnet /app/ingest/taxonomy-ingest.dll load /app/data/structure_released.xml`  |
| Health check                  | `/api/health`                                                                   |
| `ConnectionStrings__Taxonomy` | Built from the Postgres service's reference variables, over the private network |
| `PORT`                        | `8080`                                                                          |

The pre-deploy step migrates and reloads the data in one transaction, so a failed load blocks the rollout and leaves the previous release serving.

## Layout

```
apps/api/src/Taxonomy.Core         linear entries and the tree builder
apps/api/src/Taxonomy.Ingest       XML parser, migrations, bulk loader
apps/api/src/Taxonomy.Ingest.Cli   export / migrate / load
apps/api/src/Taxonomy.Api          HTTP API, also serves the web build
apps/api/openapi                   generated OpenAPI document
apps/web/src/tree                  tree model, virtualization, keyboard
apps/web/src/search                search box
apps/web/src/api/generated         Orval client
apps/web/e2e                       Playwright tests
data                               the ImageNet XML
```
