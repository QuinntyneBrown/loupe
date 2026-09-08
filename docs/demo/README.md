# Loupe demo videos

Three recordings, all against real, running software — no product responses were
mocked or invented for these takes. See "Integration substitutions and known
limitations" below for exactly what stands in for what, and why.

## Application inventory

| Application | Status | Video |
| --- | --- | --- |
| `loupe` — the Angular web client | Recorded | [loupe.webm](loupe.webm) |
| `Loupe.Api` + `Loupe.Worker` — the .NET backend and background worker | Recorded (combined; see below) | [loupe-api.webm](loupe-api.webm) |
| `design-system` — the standalone token/component reference site | Recorded | [design-system.webm](design-system.webm) |
| `Loupe.DemoIdentityProvider` | Demo-only harness, not a product application | — |
| Boards, semantic search, photographer bookmarking | Not recorded — **out of scope**, not blocked: these exist only as requirements and static HTML mockups (`docs/detailed-designs/`, `docs/mocks/`), with no Angular routes or backend endpoints yet | — |

`Loupe.Api` and `Loupe.Worker` are two independently runnable executables, but
`Loupe.Worker` has no interaction surface of its own — it only executes work the
API admits. `loupe-api.webm` demonstrates both together: real HTTP calls against
the real API, correlated with the real worker's own container log claiming and
completing each operation.

## Videos

### `loupe.webm` — the real Angular application
- **Duration / dimensions / size:** 78.8s, 1280×720, 3.53 MB
- **Poster:** [loupe-poster.png](loupe-poster.png)
- Real `Loupe.Api` + `Loupe.Worker` (in containers) + PostgreSQL + the demo
  identity provider, driven through the Angular dev server proxied same-origin
  over HTTPS. No Playwright test-mode service mocks were used — this is the
  production `app.providers.ts` composition talking to real HTTP endpoints.

Approximate chapters (verified against playback; a few seconds either way):

| Time | Chapter |
| --- | --- |
| 0:00 | Opening — the real app against the real backend |
| 0:04 | Sign in — real OpenID Connect authorization-code + PKCE redirect |
| 0:10 | Upload a photograph with a critique brief |
| 0:20 | Personal notes, saved separately from AI text |
| 0:23 | Request an AI critique — real `Loupe.Worker` lease + completion |
| 0:33 | Save a second attempt, to enable comparison |
| 0:45 | Compare two attempts side by side |
| 0:50 | Save a reference by direct image upload |
| 0:55 | Save a reference by URL, request its import (admission only — see limitations) |
| 0:65 | Delete a photograph — real worker cleanup to `Completed` |
| 0:74 | Closing |

### `loupe-api.webm` — the real API and worker
- **Duration / dimensions / size:** 63.5s, 1280×720, 5.53 MB
- **Poster:** [loupe-api-poster.png](loupe-api-poster.png)
- A local browser-based terminal display (see "Recording method" below) runs a
  **fixed** sequence of real `curl` calls against the real API, with the real
  `Loupe.Worker` container log streaming in a second pane so lease pickup and
  completion are visibly correlated with the API responses, not narrated.

Approximate chapters:

| Time | Chapter |
| --- | --- |
| 0:00 | Opening |
| 0:04 | Sign in — real OIDC code + PKCE handshake via curl |
| 0:14 | Upload a photograph (real libvips decode) |
| 0:20 | Request a critique — real worker completion, real structured result |
| 0:35 | Save a reference by URL — real SSRF-checked admission (see limitations) |
| 0:45 | Delete a photograph — real worker cleanup |
| 0:60 | Closing |

### `design-system.webm` — the standalone design system
- **Duration / dimensions / size:** 54.3s, 1280×720, 3.13 MB
- **Poster:** [design-system-poster.png](design-system-poster.png)
- The real Vite-built static site (`npm run build && npm run preview`), with
  the application and API stopped, per `design-system/README.md`.

Approximate chapters:

| Time | Chapter |
| --- | --- |
| 0:00 | Opening |
| 0:05 | A keyboard-operable primitive — real interaction, not a picture of one |
| 0:12 | The editor dialog — validation, save feedback, focus management |
| 0:22 | Design tokens — all nine categories |
| 0:38 | Component states and responsive layouts, the image grid |
| 0:50 | Closing |

## Why containers for the backend (a real finding, not a preference)

Windows has no working native `libvips` for the `NetVips` image-decoding
dependency `Loupe.Infrastructure` uses. The official `NetVips.Native.win-x64`
package (version `8.18.6`, matching the pin `AGENTS.md`/`tasks/evidence.md`
already document) **segfaults the process** on first use in this environment —
a real, reproducible native crash, not a missing-DLL error. This matches why
the product's own acceptance suite abandoned Windows-native NetVips in favor of
Linux (`tasks/evidence.md`, entry API-09: "Replaced the incomplete native
runtime with the planned Linux system libvips/libheif codec combination").

So `Loupe.Api` and `Loupe.Worker` run here in Linux containers built from
`docs/demo/harness/Dockerfile.demo-runtime`, which installs the exact same apt
package pins as `backend/Dockerfile.acceptance` (`libvips42t64=8.15.1-1.1build4`,
`libheif-plugin-libde265`/`libheif-plugin-x265`). This is demo-recording harness
only — it is not, and does not claim to be, the product's own (not yet
delivered) Compose/deployment story; see `backend/README.md`.

## Integration substitutions and known limitations

- **Identity provider.** `backend/src/Loupe.DemoIdentityProvider` is a small,
  clearly-labeled throwaway OIDC provider (discovery, JWKS, PKCE-protected
  authorize/token endpoints), built because no standalone local identity
  provider exists in this repository yet — the only prior OIDC test double
  (`ControlledIdentityProvider` in `backend/tests/Loupe.Api.Tests/Security/`)
  is an in-process `HttpMessageHandler` a real browser cannot be redirected to.
  It is never a real login and must never be deployed.
- **AI critique.** `Ai:Mode=Demo` — the product's own existing, disclosed
  local adapter (`DemoCritiqueProvider`). No AI credentials are configured and
  no third-party AI network traffic occurs, matching L2-036. Every critique
  shown is labeled "Demo · illustrative sample" on screen.
- **Reference-URL import never reaches `Completed` in these recordings.**
  This is a real product gap, discovered while building this harness, not a
  recording limitation: `Loupe.Worker`'s `Program.cs` registers only
  `CleanupWorker` and `CritiqueWorker` as hosted services, and its MediatR
  `TypeEvaluator` only admits the `Maintenance` namespace and
  `RunCritiqueCommandHandler` — there is no worker capability that claims
  `ReferenceImport`-type leases. Both videos show the real, SSRF-checked
  admission (`RequestReferenceImportCommand` succeeds, a durable `Queued`
  operation is created) and stop there deliberately, rather than fabricate a
  completion the codebase cannot currently produce.
- **Outbound reference-import fetch target.** Both recordings fetch
  `https://upload.wikimedia.org/wikipedia/commons/4/47/PNG_transparency_demonstration_1.png`,
  a stable, public, robots-permitting direct-image URL — a real outbound
  request, not a stubbed one.
- **Demo synthetic images.** All uploaded photographs/references are
  synthetic images generated for this recording (`e2e/demo/fixtures/*.jpg`),
  not real photographs.

## Recording method

- **Browser videos** (`loupe.webm`, `design-system.webm`): Playwright,
  `video: 'on'`, 1280×720, one worker, zero retries, `chromium`. Captions and
  title cards are DOM overlays injected at runtime by `e2e/demo/narration.mjs`
  (also copied to `design-system/tests/demo/narration.mjs`) — they never touch
  application source and use `pointer-events: none`.
- **`loupe-api.webm`**: no native terminal recorder or `ffmpeg` build is
  available in this environment, so per the demo-video skill's guidance, a
  small local Node server (`e2e/demo/loupe-api-terminal-server.mjs`, bound to
  `127.0.0.1` only) runs a **fixed**, non-configurable sequence of real `curl`
  child processes against the real API and streams their real stdout to a
  plain-text page (`e2e/demo/loupe-api-terminal.html`), captured the same way
  as the browser recordings. The worker's real `docker logs` output streams
  into a second pane. Command selection lives entirely in the server; the page
  accepts no input that could execute an arbitrary command.
- Every recording asserts real state (HTTP status codes, DOM text, `Completed`
  operation status) before advancing — nothing is a fixed-length sleep standing
  in for a real check.
- Posters were extracted with `e2e/demo/inspect-video.mjs` (usage:
  `node inspect-video.mjs <path-to.webm> <poster-out.png> [seek-seconds...]`),
  which loads the `.webm` in a headless browser `<video>` element, seeks to
  the given timestamp(s), and screenshots the element. The same script prints
  decoded duration and dimensions, which is how the numbers in this README
  were measured.

## Reproducing the stack from a clean environment

Prerequisites: Docker Desktop, .NET SDK `10.0.303`+ (see `global.json`), Node
`24.18.x` (see `frontend/README.md`), PowerShell 7+.

```powershell
# 1. Stand up Postgres, the demo identity provider, the containerized
#    Loupe.Api/Loupe.Worker, and the Angular dev server (HTTPS, same-origin
#    proxy). Prints the URLs to check once it's up.
pwsh docs/demo/harness/setup.ps1

# 2. Record loupe-api.webm (requires step 1's stack running)
cd e2e/demo
npx playwright test --config=playwright.demo-api.config.js

# 3. Record loupe.webm (same stack; a fresh demo identity keeps its library
#    empty regardless of what step 2 already created)
npx playwright test --config=playwright.demo-app.config.js

# 4. Record design-system.webm (standalone — stop the app/API first if you
#    want to double-check no runtime dependency exists; the config builds and
#    serves the site itself)
cd ../../design-system/tests/demo
npx playwright test --config=playwright.demo.config.js

# 5. Tear down
pwsh ../../../docs/demo/harness/teardown.ps1
```

Each Playwright config lives beside its production counterpart
(`e2e/playwright.config.js`, `design-system/playwright.config.js`) but is
entirely separate — normal acceptance pacing, retries, and video policy are
untouched. Recorded videos land under each config's own `test-results/`
(already covered by the repository's `**/test-results/` gitignore rule) before
being promoted here.

If a `loupe.webm` or `loupe-api.webm` re-run reuses the same demo Postgres
database across takes, clear the content tables first to avoid duplicate-title
ambiguity in the UI:

```sh
docker exec loupe-demo-postgres psql -U loupe -d loupe_demo \
  -c 'TRUNCATE photographs, "references", background_operations, operation_receipts, journal.deletions CASCADE;'
```

## What setup.ps1 actually does (and why)

- Trusts and exports the local ASP.NET Core HTTPS dev certificate — used by
  the demo identity provider, the containerized API's Kestrel HTTPS listener,
  and the Angular dev server, so the browser trusts all three with one cert.
- Starts a demo-only PostgreSQL container (`pgvector/pgvector:pg17`, matching
  the image family `backend/tests/Loupe.Api.Tests/PostgreSqlFixture.cs` uses)
  and applies the real EF Core migrations from
  `backend/src/Loupe.Infrastructure/Persistence/Migrations`. Applying
  migrations needs `Microsoft.EntityFrameworkCore.Design`, which
  `Loupe.Api.csproj` deliberately does not carry — the script adds it
  temporarily and reverts the file via `git checkout` immediately after,
  never leaving a diff.
- Builds `docs/demo/harness/Dockerfile.demo-runtime` and runs the real
  `Loupe.Api` and `Loupe.Worker` from it, networked with Postgres and reaching
  the host-side demo identity provider via `host.docker.internal`.
- Starts `Loupe.DemoIdentityProvider` on the host and serves the Angular app
  over HTTPS with `frontend/proxy.conf.demo.json` proxying `/api` and
  `/signin-oidc` same-origin to the API — required because the OIDC callback
  and API must share an origin with the browser page (`backend/README.md`).

`teardown.ps1` stops every container, the demo identity provider, the Angular
dev server, and removes the scratch cert/media directory. It does not touch
the repository working tree.
