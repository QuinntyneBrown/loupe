# Loupe demo videos

The existing recordings predate local authentication and show the retired external
sign-in flow. They are historical recordings, not evidence of the current login.
The rerun harness now provisions local users and uses Loupe-issued JWT sessions.
Demo-only accounts are `photographer@example.com` and `api-demo@example.com`, with
the synthetic password `local acceptance password`. Never reuse these accounts
or credentials in a deployed environment.

Four recordings, all against real, running software — no product responses were
mocked or invented for these takes. See "Integration substitutions and known
limitations" below for exactly what stands in for what, and why. The two newest,
[`inspiration/`](inspiration/README.md) and [`locations/`](locations/README.md), are
narrated tours recorded on 2026-09-12 against the current code with local JWT
sign-in: the Inspiration library with keyword search, and the Locations area with
images, live search indexing through a local embedding model, the scouting report
request, and Find a location in Keyword and Meaning modes.

## Application inventory

| Application | Status | Video |
| --- | --- | --- |
| `loupe` — the Angular web client | Recorded | [loupe.webm](loupe.webm) |
| `loupe` — Inspiration library and keyword search, narrated | Recorded 2026-09-12 (real API, worker, database; local JWT sign-in) | [inspiration/inspiration-tour.mp4](inspiration/inspiration-tour.mp4) · [player](inspiration/index.html) · [details](inspiration/README.md) |
| `loupe` — Locations, scouting report request and Find a location (keyword and meaning), narrated | Recorded 2026-09-12 (real API, worker, database, pgvector and a local Ollama `bge-m3`; no Azure OpenAI) | [locations/locations-tour.mp4](locations/locations-tour.mp4) · [player](locations/index.html) · [details](locations/README.md) · [how it was made](locations/making-of.md) |
| `Loupe.Api` + `Loupe.Worker` — the .NET backend and background worker | Recorded (combined; see below) | [loupe-api.webm](loupe-api.webm) |
| `design-system` — the standalone token/component reference site | Recorded | [design-system.webm](design-system.webm) |
| `Loupe.DemoIdentityProvider` | Demo-only harness, not a product application | — |
| Boards, tag filters, keyword search | Recorded in the Inspiration tour above | see `inspiration/` |
| Meaning search for locations, location indexing | Recorded in the Locations tour above (real local `bge-m3` embeddings) | see `locations/` |
| Scouting report generation, Meaning search for the inspiration library, photographer bookmarking, AI suggestions/summaries | Not recorded — the scouting report and AI suggestions need a configured Azure OpenAI provider (the Locations tour shows the request being refused and a synthetic design-system example); inspiration Meaning search is not implemented; photographer bookmarking is implemented but outside the tours' scope | — |

`Loupe.Api` and `Loupe.Worker` are two independently runnable executables, but
`Loupe.Worker` has no interaction surface of its own — it only executes work the
API admits. `loupe-api.webm` demonstrates both together: real HTTP calls against
the real API, correlated with the real worker's own container log claiming and
completing each operation.

## Videos

### `loupe.webm` — the real Angular application
- **Duration / dimensions / size:** 68.3s, 1280×720, 3.04 MB
- **Poster:** [loupe-poster.png](loupe-poster.png)
- Real `Loupe.Api` + `Loupe.Worker` (in containers) + PostgreSQL + the demo
  identity provider, driven through the Angular dev server proxied same-origin
  over HTTPS. No Playwright test-mode service mocks were used — this is the
  production `app.providers.ts` composition talking to real HTTP endpoints.

Approximate chapters (verified against playback; a few seconds either way):

| Time | Chapter |
| --- | --- |
| 0:00 | Opening — the real app against the real backend |
| 0:03 | Sign in — real OpenID Connect authorization-code + PKCE redirect |
| 0:09 | Upload a photograph with a critique brief |
| 0:16 | Personal notes, saved separately from AI text |
| 0:20 | Request an AI critique — real `Loupe.Worker` lease + completion |
| 0:27 | Save a second attempt, to enable comparison |
| 0:34 | Compare two attempts side by side |
| 0:41 | Save a reference by direct image upload |
| 0:46 | Save a reference by URL, request its import (admission only — see limitations) |
| 0:53 | Delete a photograph — real worker cleanup to `Completed` |
| 0:64 | Closing |

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

- **Local accounts.** The rerun harness creates accounts through `Loupe.Admin` in
  the same PostgreSQL database as the API. Password verification, JWT issuance,
  and revocation run in the production backend. The old provider harness has
  been removed. Existing videos still show its historical behavior.
- **AI critique.** Historical recordings used a deterministic sample adapter,
  which has now been removed. New runs use Azure OpenAI with `Ai:Mode=Live`; supply `Ai__Endpoint`,
  `Ai__Deployment`, `Ai__Model`, and `Ai__ApiKey` in the calling environment
  using the [backend setup guide](../../backend/README.md#azure-openai-critiques).
  The harness passes these variables by name without printing their values.
  Without the required configuration, AI requests report
  Integration not configured; manual library work remains available. Existing
  sample critiques are archived by the retirement migration. Historical videos
  are not evidence of real-provider analysis or the current critique layout.

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
# 1. Stand up Postgres, local accounts, the containerized
#    Loupe.Api/Loupe.Worker, and the Angular dev server (HTTPS, same-origin
#    proxy). Prints the URLs to check once it's up. Defaults: prefix loupe-demo,
#    Postgres 5433, Api 5001, app 4200; pass -Prefix/-DbPort/-ApiPort/-AppPort to
#    run a second, fully isolated stack beside an existing one.
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

# 5. Record the narrated Inspiration tour — its own seed/record/assemble
#    sequence and isolated stack are documented in docs/demo/inspiration/README.md

# 6. Tear down (pass the same -Prefix/-AppPort you gave setup.ps1)
pwsh ../../../docs/demo/harness/teardown.ps1
```

Each Playwright config lives beside its production counterpart
(`e2e/playwright.config.js`, `design-system/playwright.config.js`) but is
entirely separate — normal acceptance pacing, retries, and video policy are
untouched. Recorded videos land under each config's own `test-results/`
(already covered by the repository's `**/test-results/` gitignore rule) before
being promoted here.

If a re-run reuses the same demo Postgres database across takes, empty the
library content first (accounts, sessions and the worker's singleton dispatch
cursor are kept; the script refuses any container setup.ps1 did not create):

```powershell
pwsh docs/demo/harness/reset-content.ps1            # or -Prefix <your prefix>
```

## What setup.ps1 actually does (and why)

- Trusts and exports the local ASP.NET Core HTTPS development certificate for
  the containerized API and Angular dev server.
- Starts demo PostgreSQL and applies the real EF Core migrations using the
  Infrastructure design-time factory.
- Builds the API, Worker, and Admin CLI in the Linux runtime image and networks
  the API/Worker with PostgreSQL. A fresh random signing key is supplied only to
  the API through its environment; rerunning setup invalidates prior sessions.
- Provisions both demo accounts by piping synthetic passwords to the Admin CLI.
- Serves Angular over HTTPS with `/api` proxied same-origin to the API. The
  proxy configuration is generated per stack into the scratch directory
  (`%TEMP%\<prefix>-harness\proxy.conf.json`), and the dev server's output is
  kept beside it in `ng-serve.log` / `ng-serve.err.log`.

`teardown.ps1` stops that prefix's demo containers and Angular dev server and
removes its scratch cert/media directory. It does not modify repository source.
