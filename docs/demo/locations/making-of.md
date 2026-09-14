# How the Locations tour was made

This is the exact production record for `locations-tour.mp4`, step by step, with
the versions, settings and measured values of the take that shipped. The scripts
named here live in `e2e/demo/locations/`; the harness in `docs/demo/harness/`.
Nothing in the video was mocked, and nothing was edited by hand: every frame is
one Chromium screen recording of the real application, cut only into chapters.

## The voice

| | |
| --- | --- |
| Voice | **`en-US-EmmaMultilingualNeural`** — Microsoft Edge's online neural text-to-speech ("Emma") |
| Library | [`edge-tts`](https://pypi.org/project/edge-tts/) 7.2.8 (Python 3.11.1), which calls the same speech service the Edge browser's Read Aloud uses |
| Settings | `rate='-5%'` (slightly slower than the default), `boundary='WordBoundary'` so the service returns a timestamp for every spoken word |
| Input | Only the ten scene texts in `e2e/demo/locations/scenes.json` — 486 words in total; nothing from the application, the database or the images is sent to the service |
| Output | One MP3 per scene, converted by FFmpeg to 48 kHz mono WAV, plus the word timings that drive the captions |

Emma is the same voice the Inspiration tour uses, chosen because the
multilingual neural model reads product names, punctuation and short clauses
cleanly at a reduced rate. The speech was generated once; no cuts, retakes or
level edits were applied except the loudness normalisation described below.

## Tooling

| Tool | Version | Role |
| --- | --- | --- |
| Node.js | 24.18.0 | seeding, recording, player verification |
| Playwright | 1.63.0 (bundled Chromium 153.0.8010.12, revision 1243) | drives the real browser and records it |
| Python | 3.11.1 | narration, assembly, media verification |
| edge-tts | 7.2.8 | narration and word boundaries |
| imageio-ffmpeg | 0.6.0, bundling FFmpeg 7.1 (`7.1-essentials_build`) | audio conversion, encoding, captions, concatenation, verification |
| Docker Desktop | Linux containers | PostgreSQL (`pgvector/pgvector`), the real `Loupe.Api` and `Loupe.Worker`, and `ollama/ollama` (`sha256:684d8674…`) |
| Ollama model | `bge-m3` (blob `sha256:daec91ffb5dd…`, 1.16 GB, 1024 dimensions) | the real embedding model behind Meaning search and location indexing |
| Design system | `vite build` + `vite preview` on 127.0.0.1:4187 | the synthetic report page for chapter 7 |

## Step 1 — an isolated real stack

```powershell
pwsh docs/demo/harness/setup.ps1 -Prefix loupe-loc-demo -DbPort 5436 -ApiPort 5013 -AppPort 4212 -Ollama
```

The harness trusts and exports the ASP.NET dev certificate, starts PostgreSQL
with the pgvector image on 5436, applies the real EF Core migrations (including
`SearchVectors`, which runs `CREATE EXTENSION IF NOT EXISTS vector`), builds one
Linux runtime image holding the real `Loupe.Api`, `Loupe.Worker` and
`Loupe.Admin` (Linux libvips, because native NetVips crashes on Windows),
networks them, and — because of `-Ollama` — starts an `ollama/ollama` container
on the same network, waits for its server, pulls `bge-m3` into the shared
`loupe-ollama-models` volume, and points both the Api and the Worker at it
through `Embeddings__Endpoint=http://loupe-loc-demo-ollama:11434` and
`Embeddings__Model=bge-m3`. `OLLAMA_KEEP_ALIVE=24h` keeps the model resident;
without it Ollama unloads the model after five idle minutes and the first Meaning
query of a chapter waits seconds for the reload (this was found in rehearsal and
fixed before the take). No `Ai__Endpoint`, `Ai__Deployment` or `Ai__ApiKey` was
set, so the scouting report provider is deliberately unconfigured. Two local
accounts are provisioned through `Loupe.Admin`; the Angular application is served
by `ng serve` over HTTPS on 4212 with `/api` proxied same-origin to the real Api,
using the production `app.providers.ts` composition — real HTTP adapters, no
Playwright service mocks.

Separately, `npm run build && npm run preview` in `design-system/` served the
static reference site on 127.0.0.1:4187 for chapter 7.

## Step 2 — seeding through the public API

`seed.mjs` signs in the way the Angular adapter does (anonymous CSRF proof,
credential sign-in, then the identity-bound antiforgery token on every mutation)
and refuses to run unless the account holds no locations. It then creates the
seven catalogue locations from `library.json` in reverse order (so the first
catalogue entry is the newest and appears first in the grid), each with an
`Idempotency-Key`, uploads their 18 images as multipart `POST
/api/locations/{id}/images` (real libvips decoding, previews and EXIF stripping),
and finally polls `GET /api/locations/{id}` until every location reports
`indexStatus: current` — which only happens after the real worker has claimed
each `LocationIndex` operation, built the document, embedded it through Ollama
and written the vector row. The eighth catalogue entry, `Deptford creek stairs`,
is left for the recording to add through the UI.

The images are public Picsum placeholder photographs already cached for the
Inspiration tour (`e2e/demo/inspiration/images`); they stand in for location
photographs and are not pictures of the named places.

## Step 3 — rehearsal without video

```
node e2e/demo/locations/record.mjs --dry
```

The same script as the take, with every pause capped at 100 ms and no video,
runs all ten scenes and every assertion, then re-reads the persisted state
through the API. Three faults surfaced here and were fixed before any video was
recorded: tags render alphabetically after a save, search results list newest
first, and the first Meaning query stalled on Ollama reloading the model. The
rehearsal also produced `preview-*.png`, one screenshot per scene, for a first
visual check. Because the rehearsal mutates the library, the database was reset
(`reset-content.ps1`, which truncates the content tables and resets the worker's
dispatch-cursor row rather than deleting it) and reseeded before the take.

## Step 4 — narration

```
python e2e/demo/locations/narrate.py
```

For each scene, `edge_tts.Communicate(text, voice='en-US-EmmaMultilingualNeural',
rate='-5%', boundary='WordBoundary').stream()` yields audio chunks (written to
`<scene>.mp3`) interleaved with `WordBoundary` events carrying each word's
offset and duration in 100-nanosecond ticks. FFmpeg converts the MP3 to
`<scene>.wav` (48 kHz, mono). The script records `speechDuration` from the WAV
and sets each scene's on-screen `duration = max(speechDuration + 1.2 s, 10 s)`, so
the picture never cuts before the sentence ends. Everything is written to
`source/narration.json`, which the take reads to pace itself.

Measured speech per scene (seconds): sign-in 17.11, browse 18.96, add 17.09,
images 16.61, detail 15.55, report 20.47, report example 21.60, keyword 19.80,
meaning 20.62, close 19.18.

## Step 5 — the take

```
pwsh docs/demo/harness/reset-content.ps1 -Prefix loupe-loc-demo
node e2e/demo/locations/seed.mjs
node e2e/demo/locations/record.mjs
```

`record.mjs` launches Playwright's Chromium with one browser context at
1440 × 900, `recordVideo` at the same size, and three guards:

- a route interceptor that aborts and counts any request not to `localhost` or
  `127.0.0.1` (the take must make zero external requests);
- a `pageerror` listener (zero page errors allowed) and a response listener that
  fails the take on any API response ≥ 500 — except the single expected
  `503 integration_not_configured` on `POST /api/locations/{id}/scouting-report`,
  which is counted separately and must occur exactly once;
- an init script that draws a small blue pointer dot following the mouse, so
  clicks are visible in the recording.

Before the first scene the page is filled solid magenta (`#ff00ff`) for two
seconds and then white; the assembly step finds the last magenta frame in the
recording to align the video timeline with the script's timestamps (measured
anchor: 1.88 s). Each scene then runs its interactions through the same page
objects the acceptance suite uses (`e2e/page-objects/*`, extended in
`tour-page.js` with typed input at 55 ms per key and image-decode checks), waits
until the narration's duration has elapsed, takes a `frame-<scene>.png`
screenshot, and appends `{start, end}` to the timeline. The ten scenes, with what
each asserts in the browser:

1. **Sign in** — real `/api/session/csrf` → `sign-in` → `session`; Locations opens with 7 cards and the summary `7 locations`.
2. **Browse** — hovering three cards reveals `Locality · N images · No scouting report`; the `Find a location` header link is present.
3. **Add** — the Add location dialog is typed live (name, locality, region, country, setting, brief, notes, three tags); after Save the notice reads `“Deptford creek stairs” saved.`, the count is 8, the new card is first with the `No images yet` placeholder.
4. **Images** — three real JPEG uploads through the images dialog; gallery of 3 with `Image 1, cover`; ArrowRight twice moves the selection to 2 and 3; `Set as cover` makes image 3 the cover.
5. **Detail** — notes appended and saved (`Saved`); the `Updating search` pill must appear and then disappear (up to 30 s allowed) as the real worker embeds the location; tag `mud` saved and listed.
6. **Report** — the disclosure names Azure OpenAI, the three images, brief and camera settings; `Request scouting report` is refused with `Integration not configured…` and the notes remain editable.
7. **Report example** — navigates to the design-system page, checks the six section headings in order, and clicks `Show image 3`, which selects gallery image 3 there.
8. **Keyword** — `arches` → 2 results (Kew, St Dunstan); Setting `Outdoor` → 1; `Any` → 2; blank query → 8; Tags dialog → `river` → 2 (Deptford, Kew) with the new location labelled `No scouting report`.
9. **Meaning** — `Clear all`, Meaning with a blank query shows the prompt and makes no request; the query `somewhere calm by the water for two people at first light` returns results labelled `Matched by meaning`, and the script asserts the first result is one of the three waterside locations. The order the model produced — Kew Bridge foreshore, Hampstead ponds path, Walthamstow wetlands, Window-lit studio, Deptford creek stairs, Peckham multi-storey roof, St Dunstan in the East, Cornmill Gardens bandstand — was recorded, not chosen.
10. **Close** — back on Locations: 8 locations, the new card with its cover and `3 images`.

After the browser closes, the script saves the raw capture as
`source/desktop.webm` (VP8/Opus, 1440 × 900, 14.8 MB) and re-reads the outcome
through the API with the same client the seed used: 8 locations; the added
location has 3 images with the third as cover, the appended notes, the `mud`
tag, `indexStatus: current`, `reportStatus: None`; `query=&tags=river` returns
2; and the Meaning query returns exactly the order shown in the take. All of it,
with the timeline, is written to `source/recording.json`.

## Step 6 — assembly

```
python e2e/demo/locations/assemble.py
```

1. **Refuse a bad take.** The script asserts the recording has zero errors,
   zero external requests, zero unexpected 5xx, and that the persisted checks
   passed; otherwise it stops.
2. **Find the anchor.** The first five seconds of `desktop.webm` are decoded at
   25 fps and scaled to a single pixel each; the last magenta pixel marks where
   the script's clock starts (1.88 s into the file).
3. **Cut one clip per scene.** Each scene's `[start, end]` from the timeline is
   rounded up to a whole frame at 25 fps (chapter lengths: 18.36, 20.20, 18.32,
   17.84, 16.76, 21.72, 22.84, 21.04, 21.84 and 20.40 s — 199.32 s in total); the
   script checks the speech fits inside each.
4. **Frame the picture.** FFmpeg pads the 1440 × 900 capture to 1440 × 1040 with
   the interface placed 44 px down, paints a 44 px header strip and a 96 px
   caption strip (`#172027`), and burns an ASS subtitle file onto the frame:
   the header reads `NN / chapter title` (Segoe UI 18), a badge sits top-right
   (Segoe UI 16) — `REAL API / REAL DATABASE / LOCAL EMBEDDINGS / NO AZURE
   OPENAI` on every chapter except the seventh, which reads `DESIGN SYSTEM /
   SYNTHETIC EXAMPLE / NOT THE APPLICATION` — and the captions (Segoe UI 28,
   white, centred at y = 991, wrapped at 82 characters) are placed in the strip
   below the interface so they never cover it.
5. **Time the captions from the voice.** Word boundaries are grouped into cues
   that close at a sentence end, at 65 characters, or after 4.3 s; each cue
   starts at its first word's offset and ends 120 ms after its last word
   (clamped to the next word). This produced 52 cues.
6. **Encode.** Video: libx264, preset `fast`, CRF 20, `yuv420p`, 25 fps.
   Audio: the scene's WAV normalised with `loudnorm` (I = −16 LUFS, TP = −1.5 dB,
   LRA = 11), padded and trimmed to the clip length, AAC 192 kb/s at 48 kHz.
7. **Join without re-encoding.** The ten clips are concatenated with the
   FFmpeg concat demuxer (`-c copy -movflags +faststart`) into
   `locations-tour.mp4`.
8. **Sidecars.** `captions.srt` and `captions.vtt` are written from the cues
   with chapter offsets applied; `chapters.json` and `transcript.md` from the
   chapter list; `poster.jpg` is the frame two seconds before the end of
   chapter 9; and the chapter buttons are injected into `index.html`.

## Step 7 — verification

- `node e2e/demo/locations/verify-player.mjs` loads `index.html` in Chromium,
  checks the video reports 1440 × 1040 and a duration within 0.3 s of the chapter
  sum (measured 199.34 s), plays it, and clicks every chapter button, requiring
  the player to land within two seconds of each chapter's start.
- `python e2e/demo/locations/verify-media.py` decodes the whole file with FFmpeg
  in error-only mode (no output allowed), measures the audio (mean −16.2 dB, peak
  −1.5 dB, both inside the accepted band), and extracts one frame per chapter
  (`source/final-*.jpg`) for visual review.
- Frames for chapters 5, 7 and 9 were reviewed by eye: the `Updating search`
  pill live beside the new cover, the design-system badge and report pills, and
  the `Matched by meaning` results, with the header and captions in their own
  strips outside the recorded interface.

The full record of the shipped take is in `verification.md`; the raw capture,
narration audio, subtitle files, per-chapter clips and machine output stay in the
ignored `source/` directory.

## Step 8 — tear down

```
pwsh docs/demo/harness/teardown.ps1 -Prefix loupe-loc-demo -AppPort 4212
```

removes the four containers (Postgres, Api, Worker, Ollama), the network, the
Angular dev server and the scratch certificates and media; the shared model
volume is kept so the next `-Ollama` stack does not pull 1.2 GB again.
