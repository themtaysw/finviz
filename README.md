# ImageNet Taxonomy Explorer

## Running it

The whole stack in Docker:

```bash
docker compose up --build
```

Then open http://localhost:8080. Compose starts Postgres, runs the ingest once (migrations plus loading the XML, about a second) and starts the app only after that succeeded.

The app is a single image: the .NET API also serves the built React app, so there is one origin, no proxy and no CORS. The same image contains the ingest CLI and the XML, which is what a deploy runs before switching traffic.

### Developing

Prerequisites: Docker, .NET 10 SDK, Node 22+ with pnpm.

```bash
docker compose up -d --wait db
dotnet run --project apps/api/src/Taxonomy.Ingest.Cli -- load data/structure_released.xml
dotnet run --project apps/api/src/Taxonomy.Api
pnpm --dir apps/web install
pnpm --dir apps/web dev
```

API: http://localhost:5080 · Web with hot reload: http://localhost:5173 (proxies `/api` to the API)

Postgres is exposed on host port `5433` to avoid clashing with a local install. Override with `DB_PORT`.

## Data

`data/structure_released.xml` is vendored from [tzutalin/ImageNet_Utils](https://github.com/tzutalin/ImageNet_Utils/blob/master/detection_eval_tools/structure_released.xml) so the build is reproducible offline.

Export it to the linear `(name, size)` form:

```bash
dotnet run --project apps/api/src/Taxonomy.Ingest.Cli -- export data/structure_released.xml linear.json
```

`load` applies pending migrations and then replaces the stored taxonomy in a single transaction, so it is safe to re-run.

## API

| Endpoint | Purpose |
| --- | --- |
| `GET /api/nodes/roots` | Top-level nodes |
| `GET /api/nodes/{id}` | One node with its path and ancestors, for breadcrumbs and for expanding to a search hit |
| `GET /api/nodes/{id}/children?offset&limit&order` | One page of children, ordered by `source`, `name` or `size` |
| `GET /api/nodes/{id}/tree?depth` | The subtree as nested nodes, built by `TaxonomyTreeBuilder` |
| `GET /api/search?q&limit` | Label search ranked by exact synonym, then synonym prefix, then subtree size |
| `GET /api/health` | Readiness, including a database round trip |

The OpenAPI document is written to `apps/api/openapi/taxonomy.json` on every build and committed, so a contract change shows up in review as a diff. The web client (types plus TanStack Query options and hooks) is generated from it with Orval: `pnpm --dir apps/web generate:api`, and `check:api` fails if the committed client has drifted from the spec.

Every response carries `childCount` so a client can size a node's child list before fetching any of it. Queries run in single-digit milliseconds; the worst case measured is a single-character search matching 44k rows at ~30 ms.

**Cancellation.** Every handler takes the request's cancellation token and passes it to Npgsql, so a client that disconnects also stops the query on the server. `RequestCancellationTests` proves it: it blocks a query behind an exclusive table lock, drops the request, then asks `pg_stat_activity` whether the query is gone. It runs against a real Kestrel socket on purpose - the in-memory test server tears down in-flight work by itself and would pass even if the application ignored the token.

## Rendering the tree

The tree's shape is known before any of it is loaded. Every node carries `childCount`, and the client only remembers which nodes are expanded (with their position among their siblings). From that alone it computes the full list of visible rows: a parent, a position and a depth per row.

- **Virtualized.** Only the rows in and around the viewport are in the DOM, about 45, whether a node has 3 children or 2,350.
- **Paged by viewport.** Row content is fetched in pages of 100, and only for the pages the rendered rows fall in. Opening a deep link into `Misc` loads 3 pages instead of 24.
- **Cancelled when scrolled past.** A page that leaves the viewport before it arrives loses its last observer, TanStack Query aborts the request, and the API stops the query in Postgres.
- **Scroll to a node without its data.** A deep link's row index comes from structure alone, so the list can scroll to it before its page is loaded.
- **Keyboard.** The tree follows the WAI-ARIA tree pattern: arrows, Home/End, Page Up/Down, Right to expand or step in, Left to collapse or go to the parent, Enter to select. It uses `aria-activedescendant` rather than moving focus between rows, because a focused row could be unmounted by virtualization mid-scroll.
- **Cheap re-renders.** Rows are memoized and receive only primitives and stable callbacks, so scrolling re-renders only rows whose data changed.

## Search

Labels are comma-separated synonyms (`dog, domestic dog, Canis familiaris`), so ranking treats each synonym as a name: an exact synonym match first, then a synonym that starts with the query, then larger subtrees before smaller ones. Searching "dog" returns the animal before the slang for a person, which a plain "shortest label first" ranking got backwards.

In the UI the search box is an ARIA combobox (arrow keys, Enter, Escape, `/` to focus):

- **Debounced**, so a burst of typing sends one request.
- **Cancelled when stale.** When the query changes while a request is in flight, TanStack Query aborts it and the API cancels the SQL.
- **No flicker.** The previous results stay visible, dimmed, until the new ones arrive.
- Each result shows the matched text highlighted and a short trail of its ancestors, which is what tells the four `dog, domestic dog` entries apart. Choosing one selects it and reveals it in the tree.

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

```bash
pnpm --dir apps/web test
```

Component tests run in jsdom against an MSW fake of the API.

```bash
pnpm --dir apps/web e2e
```

End-to-end tests drive Google Chrome against the real stack: the loaded Postgres from `docker compose`, the API (started automatically if it isn't running) and the production build of the web app. They cover search, deep links into a 2,350-item list, keyboard-only use, and a search request actually being aborted in the browser when a newer query overtakes it.
