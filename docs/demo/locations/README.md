# Locations, scouting reports and Find a location — narrated demo

Open `index.html` for the chaptered player, or play `locations-tour.mp4`
directly. `transcript.md` holds the narration, `captions.srt`/`captions.vtt` the
timed captions (also burned into the strip below the interface), `chapters.json`
the chapter metadata, and `verification.md` the measured verification record.

| Application | Purpose | Status |
| --- | --- | --- |
| `loupe` — the Angular web client, Locations area and Find a location | Keep shoot locations with images, request a scouting report, find a location by keyword or meaning | **Recorded** — `locations-tour.mp4` |
| `Loupe.Api` + `Loupe.Worker` + PostgreSQL (pgvector) + Ollama `bge-m3` | The real backend the recording runs against (containers from `docs/demo/harness`, `-Ollama`) | Running dependency, not separately recorded here (see [`../README.md`](../README.md)) |
| `design-system` — the standalone reference site | A synthetic scouting report, shown because no AI provider is configured | One chapter, badged `DESIGN SYSTEM / SYNTHETIC EXAMPLE / NOT THE APPLICATION` |

- **Measured:** 199.3 s, 1440 × 1040, 13.96 MB (`locations-tour.mp4`), H.264/AAC, 25 fps;
  narration mean −16.2 dB, peak −1.5 dB; 52 caption cues. Poster: [`poster.jpg`](poster.jpg)
  (a frame extracted from the encoded video near the end of chapter 9, the Meaning results).
- **Truthfulness:** one continuous Chromium take of the real Angular application
  (production `app.providers.ts` composition, real HTTP adapters, real Loupe-issued
  JWT sign-in) against the real `Loupe.Api`, `Loupe.Worker`, PostgreSQL with pgvector
  and a local Ollama `bge-m3` container. No Playwright service fixtures, no mocked
  product responses. Every outcome shown is asserted in the browser and then re-read
  through the API after the take (`source/recording.json`, `persisted`).
- **Header badge:** `REAL API / REAL DATABASE / LOCAL EMBEDDINGS / NO AZURE OPENAI` —
  the embedding model is real and local; no Azure OpenAI credentials are configured,
  so the scouting report request is shown being refused (chapter 6), and chapter 7
  switches to the design-system site, badged separately, for a synthetic report.
- **Narration:** synthetic, Microsoft Edge `en-US-EmmaMultilingualNeural` via
  `edge-tts`; only the authored text in `e2e/demo/locations/scenes.json` is sent.
- **Source revision:** recorded on the `feat/locations` branch at the commit that
  adds this recording (`docs/demo/harness/*`, `e2e/demo/locations/*`).

## Chapters (verified against playback)

| Time | Chapter | What is demonstrated and asserted |
| --- | --- | --- |
| 0:00 | Places, seen before the shoot | Real credential sign-in (`/api/session/csrf` → `sign-in` → `session`); lands on Locations with 7 seeded locations |
| 0:18 | Browse the Locations library | Grid of 7 cards; hovering shows `Locality · N images · No scouting report`; the `Find a location` header link |
| 0:39 | Add a location | Add location dialog typed live (name, locality, region, country, setting, brief, notes, three tags) → notice `“Deptford creek stairs” saved.`; 8 locations; the new card is first with the `No images yet` placeholder |
| 0:57 | Upload images and choose a cover | Three real uploads (real libvips previews) through the images dialog; gallery of 3 with `Image 1, cover`; arrow keys move the selection to 2 and 3; `Set as cover` makes image 3 the cover |
| 1:15 | Notes, tags, and a search index that keeps up | Notes appended and saved (`Saved`); the `Updating search` pill appears, then clears when the real worker publishes the vector through Ollama; tag `mud` added and saved |
| 1:31 | Ask for a scouting report | The disclosure names Azure OpenAI, the 3 images, brief and camera settings and what stays on the server; `Request scouting report` → `Integration not configured…` with editing still available (the one expected 503) |
| 1:53 | What a scouting report looks like | Design-system `/locations.html`: six sections in order, rating and evidence-basis pills, `Show image 3` selects gallery image 3 — synthetic, badged as such |
| 2:16 | Find a location by keyword | `arches` → 2 locations (Kew, St Dunstan); Setting `Outdoor` → 1; `Any` → 2; blank query → all 8; Tags dialog → `river` → 2 (Deptford, Kew), the new one labelled `No scouting report` |
| 2:37 | Find a location by meaning | `Clear all` → Meaning with a blank query asks for words; `somewhere calm by the water for two people at first light` → `Matched by meaning`, ranked by the real model: Kew Bridge foreshore, Hampstead ponds path, Walthamstow wetlands first |
| 2:59 | Saved for real | Back on Locations: 8 locations, the new card with its cover and `3 images` |

Chapter start times come from the recording timeline (`source/recording.json`)
after the calibration-frame offset is removed; `verify-player.mjs` seeks the
encoded file to every chapter and checks the player lands within two seconds.

## Data shown

- Location images are public Picsum placeholder photographs cached in
  `e2e/demo/inspiration/images` (original URLs in that folder's `images.json`).
  They are stand-ins, not photographs of the named places.
- Names, addresses, coordinates, briefs, notes and tags are fictional
  demonstration labels from `e2e/demo/locations/library.json`, not scouting advice.
- Demo account `photographer@example.com` / `local acceptance password`,
  provisioned by the harness through `Loupe.Admin`. Never reuse these in a
  deployed environment.

## Reproduce

Prerequisites: Docker Desktop (Linux containers), .NET SDK 10.0.303+ (`global.json`),
`dotnet-ef` (installed by setup if missing), Node 24.18.x, PowerShell 7+, Python 3.11
with `python -m pip install edge-tts imageio-ffmpeg`, and `npm ci` in `frontend/`,
`e2e/` and `design-system/`. Windows needs the Linux containers because native
`NetVips` crashes there (see `../README.md`). The first `-Ollama` run pulls the
1.2 GB `bge-m3` model into the shared `loupe-ollama-models` volume.

All commands run from the repository root. The stack uses its own container
prefix and ports so it never touches another running demo stack.

```powershell
# 1. Stand up an isolated stack: Postgres/pgvector (5436), real Api (5013) + Worker,
#    an Ollama container with bge-m3, demo accounts, Angular over HTTPS with /api proxied (4212).
pwsh docs/demo/harness/setup.ps1 -Prefix loupe-loc-demo -DbPort 5436 -ApiPort 5013 -AppPort 4212 -Ollama
#    Wait until https://localhost:4212/locations returns 200
#    (ng serve output: %TEMP%\loupe-loc-demo-harness\ng-serve.log).

# 2. Serve the design-system site for chapter 7 (synthetic report example).
cd design-system; npm run build; npm run preview   # http://127.0.0.1:4187 — leave running
cd ..

# 3. Trust the harness dev certificate for Node, then seed the library through the real API
#    (waits until the worker has embedded every location).
$env:NODE_EXTRA_CA_CERTS = "$env:TEMP\loupe-loc-demo-harness\certs\devcert.crt"
$env:API_URL = 'https://localhost:5013'; $env:APP_URL = 'https://localhost:4212'; $env:APP_ORIGIN = $env:APP_URL
node e2e/demo/locations/seed.mjs          # refuses to run unless the account's Locations library is empty

# 4. Rehearse every interaction and assertion without video (mutates the library).
node e2e/demo/locations/record.mjs --dry

# 5. Narration audio and word timings (Edge TTS).
python e2e/demo/locations/narrate.py

# 6. Fresh state, then the real take, assembly and verification.
pwsh docs/demo/harness/reset-content.ps1 -Prefix loupe-loc-demo
node e2e/demo/locations/seed.mjs
node e2e/demo/locations/record.mjs
python e2e/demo/locations/assemble.py
node e2e/demo/locations/verify-player.mjs
python e2e/demo/locations/verify-media.py   # then inspect source/final-*.jpg

# 7. Tear down (containers incl. Ollama, network, Angular dev server, scratch certs/media),
#    and stop the design-system preview.
pwsh docs/demo/harness/teardown.ps1 -Prefix loupe-loc-demo -AppPort 4212
```

Environment variables read by the scripts (all optional, defaults shown):
`APP_URL=https://localhost:4212`, `API_URL=https://localhost:5013`,
`APP_ORIGIN=https://localhost:4212`, `DESIGN_SYSTEM_URL=http://127.0.0.1:4187`,
`DEMO_EMAIL=photographer@example.com`, `DEMO_PASSWORD=local acceptance password`.
Backend configuration names are the harness's (`ConnectionStrings__Library`,
`Media__Root`, `Jwt__SigningKey`, `Browser__AllowedOrigins__0`, `Ai__Mode`,
`Ai__Endpoint`, `Ai__Deployment`, `Ai__Model`, `Ai__ApiKey`, `Imports__Mode`,
`Embeddings__Endpoint`, `Embeddings__Model`); the Azure values are passed through
from the calling environment by name and were unset for this recording, and the
`-Ollama` switch sets the embedding values.

Recording order: `seed.mjs` must precede `record.mjs`; `record.mjs` adds the
`liveAdd` catalogue entry itself and asserts the seeded counts, so re-run
`reset-content.ps1` + `seed.mjs` before every take. Raw capture, narration audio,
calibration frames, review frames and machine verification output live in the
ignored `source/` directory.

## Integration substitutions and limitations

- **No Azure OpenAI provider configured.** The stack runs with `Ai__Mode=Live`
  but without endpoint or key, exactly as documented in `backend/README.md`. The
  scouting report request is made for real and refused with
  `503 integration_not_configured`; the recorder counts that one response as the
  expected refusal and fails on any other 5xx. No report is generated, so every
  card reads `No scouting report` and the report-based filters (shoot type,
  people, time of day) are described but not exercised.
- **The finished report is a synthetic example.** Chapter 7 leaves the application
  for the design-system site (`design-system/locations.html`), which carries its
  own badge; nothing there is product output.
- **Local embeddings are real.** The `bge-m3` model runs in an `ollama/ollama`
  container beside the API; every location was embedded by the real worker and
  the Meaning ranking shown is the model's own, unedited. Relevance for a real
  library is the reviewer-run evaluation in `docs/evaluation/shoot-planning/`,
  not this recording.
- **Observed product quirk, not repaired:** after each full navigation the app
  focuses `<main>`, and the browser scrolls it under the sticky header so the
  page heading is briefly hidden. The take scrolls back to the top as a viewer
  would; the behaviour is visible for a moment at 0:18 and 2:59.
- **Delete, edit-details and the report lifecycle** (Outdated, Regenerate, Retry)
  are implemented and covered by the acceptance suites but are not part of this
  recording's scope.
