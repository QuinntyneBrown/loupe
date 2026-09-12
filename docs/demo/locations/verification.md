# Recording verification

Verified on 2026-09-12 using Chromium (Playwright 1.63.0) and FFmpeg 7.1 (imageio-ffmpeg).

- Stack: isolated `loupe-loc-demo` containers built from the working tree
  (`docs/demo/harness/setup.ps1 -Prefix loupe-loc-demo -DbPort 5436 -ApiPort 5013 -AppPort 4212 -Ollama`),
  real EF Core migrations (including `SearchVectors` with the pgvector extension),
  real `Loupe.Api`/`Loupe.Worker`, an `ollama/ollama` container holding `bge-m3`
  (`OLLAMA_KEEP_ALIVE=24h` so the model stays resident between chapters), Angular
  over HTTPS on 4212, and the design-system preview on 4187. Older `loupe-demo-*`
  containers were left untouched.
- Seeding through the real API: 7 locations, 18 image uploads with real libvips
  processing, 21 tags; `seed.mjs` waited until the real worker had embedded all 7
  (`indexStatus: current`) and refuses a non-empty library.
- Rehearsal (`record.mjs --dry`) and the timed take both passed every browser
  assertion: sign-in, 7 → 8 locations with the new card first and its placeholder,
  3 uploads → gallery of 3, arrow-key selection 1 → 2 → 3, cover moved to image 3,
  notes `Saved` then `Updating search` shown and cleared by the worker, tag `mud`
  saved, the report disclosure and the `Integration not configured` refusal with
  notes still editable, six report sections and a cite selecting gallery image 3 on
  the design-system page, keyword results 2 / 1 / 2 / 8 / 2 with the expected names,
  the Meaning blank-query prompt, and Meaning results labelled `Matched by meaning`
  with a waterside location first.
- Persisted state re-read through the API after the take: 8 locations; the added
  location has 3 images with the third as cover, the appended notes, the `mud` tag,
  `indexStatus: current` and `reportStatus: None`; `query=&tags=river` returns 2;
  the Meaning query returns exactly the order shown in the take
  (Kew Bridge foreshore, Hampstead ponds path, Walthamstow wetlands, Window-lit
  studio, Deptford creek stairs, Peckham multi-storey roof, St Dunstan in the East,
  Cornmill Gardens bandstand).
- Zero page errors, zero non-localhost requests (all blocked and counted), zero
  unexpected API responses ≥ 500; exactly one expected `503` on
  `POST /api/locations/{id}/scouting-report` (`integration_not_configured`).
- Encoded MP4: 199.34 s, 1440 × 1040; the chaptered player loads it, plays, and
  every one of the ten chapter buttons seeks within two seconds of its start.
- Complete video/audio decode without errors. Narration mean −16.2 dB, peak −1.5 dB;
  52 caption cues.
- Ten chapter frames were extracted from the encoded file; the frames for chapters
  5 (`Updating search` pill live beside the cover thumbnail, header and caption strips
  in place), 7 (design-system badge `DESIGN SYSTEM / SYNTHETIC EXAMPLE / NOT THE
  APPLICATION`, report sections and pills legible) and 9 (rehearsal preview: the
  Meaning results with the `Matched by meaning` pill and the ranked cards) were
  visually reviewed; header and captions stay in their own strips outside the
  recorded interface.
- Poster: frame extracted from the encoded video two seconds before the end of
  chapter 9 (Meaning results).
- Two rehearsal faults were corrected before the take and recorded here: the
  result cards list newest first (the assertion order was fixed), and the first
  Meaning query after a few idle minutes waited on Ollama reloading the model, so
  the harness now keeps the model resident and the tour allows a cold model.

Machine output (`recording.json`, `playback-check.json`, `media-check.json`,
`final-*.jpg`, `preview-*.jpg`) is in the ignored `source/` directory.
