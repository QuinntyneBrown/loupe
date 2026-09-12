# Inspiration library and keyword search — narrated demo

Open `index.html` for the chaptered player, or play `inspiration-tour.mp4`
directly. `transcript.md` holds the narration, `captions.srt`/`captions.vtt` the
timed captions (also burned into the strip below the interface), `chapters.json`
the chapter metadata, and `verification.md` the measured verification record.

| Application | Purpose | Status |
| --- | --- | --- |
| `loupe` — the Angular web client, Inspiration library and Search areas | Browse, save, organise, tag and find saved references | **Recorded** — `inspiration-tour.mp4` |
| `Loupe.Api` + `Loupe.Worker` + PostgreSQL | The real backend the recording runs against (containers from `docs/demo/harness`) | Running dependency, not separately recorded here (see [`../README.md`](../README.md)) |

- **Measured:** 142.4 s, 1440 × 1040, 12.9 MB (`inspiration-tour.mp4`), H.264/AAC, 25 fps;
  narration mean −16.3 dB, peak −1.5 dB; 41 caption cues. Poster: [`poster.jpg`](poster.jpg)
  (a frame extracted from the encoded video at 0:26, chapter 2).
- **Truthfulness:** one continuous Chromium take of the real Angular application
  (production `app.providers.ts` composition, real HTTP adapters, real Loupe-issued
  JWT sign-in) against the real `Loupe.Api`, `Loupe.Worker` and PostgreSQL. No
  Playwright service fixtures, no mocked product responses. Every outcome shown
  is asserted in the browser and then re-read through the API after the take
  (`source/recording.json`, `persisted`).
- **Header badge:** `REAL API / REAL DATABASE / NO AI CONFIGURED` — no AI provider
  is configured for this stack and no AI request is made (see limitations).
- **Narration:** synthetic, Microsoft Edge `en-US-EmmaMultilingualNeural` via
  `edge-tts`; only the authored text in `e2e/demo/inspiration/scenes.json` is sent.
- **Source revision:** recorded at `85ed902` plus the harness/demo changes
  delivered with this recording (`docs/demo/harness/*`, `e2e/demo/inspiration/*`).

## Chapters (verified against playback)

| Time | Chapter | What is demonstrated and asserted |
| --- | --- | --- |
| 0:00 | A private library, running for real | Real credential sign-in (`/api/session/csrf` → `sign-in` → `session`); lands on Inspiration with 15 references on 3 boards |
| 0:14 | Browse the Inspiration library | Board sidebar counts (Architecture 4, Streets from above 2, Window light 3), tag chips, hover overlays with title, photographer and the real `Open source page` link |
| 0:32 | Save a reference by uploading an image | Save reference → Upload an image → real draft upload and libvips preview → title/photographer → board `Window light` → Save; grid shows 16, first card is the new one, Window light count 4 |
| 0:48 | Organise references into boards | New board `Portrait studies` (0) → two cards added from their `Add to boards` action (1, 2) → board view lists exactly those two |
| 1:02 | Filter the collection by tag | `window light` chip pressed → 3 references; pressed again → 16 |
| 1:15 | Tags and notes on a reference | `Doorway, late light` detail: image, photographer link, board link, source; tag `stone` added (`Saved`); note appended and saved (`Saved`, Save disabled) |
| 1:32 | Keyword search across the library | `window light` → "Showing 3 of 3 results"; `Lindqvist` → 3 results including the photographer card (1 linked reference) |
| 1:50 | Narrow results by board | `light` → 10 results; Boards and tags → `Architecture` → 3 results (Concrete apex, Doorway, Facade); opening Doorway shows the `stone` tag |
| 2:07 | Saved for real | Back on Inspiration: 16 references on 4 boards, Portrait studies 2 |

Chapter start times come from the recording timeline (`source/recording.json`)
after the calibration-frame offset is removed; `verify-player.mjs` seeks the
encoded file to every chapter and checks the player lands within two seconds.

## Data shown

- 16 public Picsum placeholder photographs cached in `e2e/demo/inspiration/images`
  (original URLs in `images.json`, also saved as each reference's source URL).
- Titles, notes, tags, boards and photographer names are fictional demonstration
  labels from `e2e/demo/inspiration/library.json`, not verified credits.
- Demo account `photographer@example.com` / `local acceptance password`,
  provisioned by the harness through `Loupe.Admin`. Never reuse these in a
  deployed environment.

## Reproduce

Prerequisites: Docker Desktop (Linux containers), .NET SDK 10.0.303+ (`global.json`),
`dotnet-ef` (installed by setup if missing), Node 24.18.x, PowerShell 7+, Python 3.11
with `python -m pip install edge-tts imageio-ffmpeg`, and `npm ci` in both
`frontend/` and `e2e/`. Windows needs the Linux containers because native
`NetVips` crashes there (see `../README.md`).

All commands run from the repository root. The stack uses its own container
prefix and ports so it never touches another running demo stack.

```powershell
# 1. Stand up an isolated stack: Postgres (5434), real Api (5011) + Worker,
#    demo accounts, Angular over HTTPS with /api proxied (4210).
pwsh docs/demo/harness/setup.ps1 -Prefix loupe-insp-demo -DbPort 5434 -ApiPort 5011 -AppPort 4210
#    Wait until https://localhost:4210/inspiration returns 200
#    (ng serve output: %TEMP%\loupe-insp-demo-harness\ng-serve.log).

# 2. Trust the harness dev certificate for Node, then seed the library through the real API.
$env:NODE_EXTRA_CA_CERTS = "$env:TEMP\loupe-insp-demo-harness\certs\devcert.crt"
node e2e/demo/inspiration/seed.mjs          # refuses to run unless the account's library is empty

# 3. Rehearse every interaction and assertion without video (mutates the library).
node e2e/demo/inspiration/record.mjs --dry

# 4. Narration audio and word timings (Edge TTS).
python e2e/demo/inspiration/narrate.py

# 5. Fresh state, then the real take, assembly and verification.
pwsh docs/demo/harness/reset-content.ps1 -Prefix loupe-insp-demo
node e2e/demo/inspiration/seed.mjs
node e2e/demo/inspiration/record.mjs
python e2e/demo/inspiration/assemble.py
node e2e/demo/inspiration/verify-player.mjs
python e2e/demo/inspiration/verify-media.py   # then inspect source/final-*.jpg

# 6. Tear down (containers, network, Angular dev server, scratch certs/media).
pwsh docs/demo/harness/teardown.ps1 -Prefix loupe-insp-demo -AppPort 4210
```

Environment variables read by the scripts (all optional, defaults shown):
`APP_URL=https://localhost:4210`, `API_URL=https://localhost:5011`,
`DEMO_EMAIL=photographer@example.com`, `DEMO_PASSWORD=local acceptance password`.
Backend configuration names are the harness's (`ConnectionStrings__Library`,
`Media__Root`, `Jwt__SigningKey`, `Browser__AllowedOrigins__0`, `Ai__Mode`,
`Ai__Endpoint`, `Ai__Deployment`, `Ai__Model`, `Ai__ApiKey`, `Imports__Mode`); the
AI values are passed through from the calling environment by name and were unset
for this recording.

Recording order: `seed.mjs` must precede `record.mjs`; `record.mjs` uploads the
`liveUpload` catalogue entry itself and asserts the seeded counts, so re-run
`reset-content.ps1` + `seed.mjs` before every take. Raw capture, narration audio,
calibration frames, review frames and machine verification output live in the
ignored `source/` directory.

## Integration substitutions and limitations

- **No AI provider configured.** The stack runs with `Ai__Mode=Live` but without
  endpoint or key, exactly as documented in `backend/README.md`. The reference page
  shows the real `AI suggestions — Generate suggestions` control and the worker
  logs "Live image analysis requires Ai:Endpoint…"; the recording never requests
  suggestions, tags are added manually, and the badge says so.
- **Keyword search only.** Meaning (semantic) search is not part of this
  recording; the Search page's hint reads "Keyword matches the words you type in
  your saved library" and that is what is demonstrated.
- **Save from a link and source import are not demonstrated.** Both controls are
  visible (`From a link`, `Request import`) but never submitted, so no outbound
  request to a third-party site is made during the take. Upload is the save path
  shown.
- **Observed product quirk, not repaired:** after each full navigation the app
  focuses `<main>`, and the browser scrolls it under the sticky header so the
  page heading is briefly hidden. The take scrolls back to the top as a viewer
  would; the behaviour is visible for a moment at 0:14 and 2:07.
- **Photographer portfolio URLs** (`https://maralindqvist.example`,
  `https://tomasferreira.example`) are reserved `.example` hosts; nothing fetches
  them (photographer summaries need the unconfigured AI provider).
