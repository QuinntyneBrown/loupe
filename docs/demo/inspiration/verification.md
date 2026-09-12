# Recording verification

Verified on 2026-09-12 using Chromium (Playwright 1.63.0) and FFmpeg 7.1 (imageio-ffmpeg).

- Stack: isolated `loupe-insp-demo` containers built from the working tree
  (`docs/demo/harness/setup.ps1 -Prefix loupe-insp-demo -DbPort 5434 -ApiPort 5011 -AppPort 4210`),
  real EF Core migrations, real `Loupe.Api`/`Loupe.Worker`, Angular over HTTPS on 4210.
  The pre-existing `loupe-demo-*` containers (27 hours old, older code and identity
  configuration) were left untouched.
- Seeding through the real API: 15 image uploads with real libvips processing,
  3 boards, 2 linked photographers (`seed.mjs`; refuses a non-empty library).
- Rehearsal (`record.mjs --dry`) and the timed take both passed every browser
  assertion: sign-in, 15 → 16 references, first card is the upload, board counts
  3 → 4 and 0 → 1 → 2, tag filter 3 → 16, tag `stone` saved, note saved, search
  totals 3 / 3 / 10 / 3, result titles, and the `stone` tag on the opened result.
- Persisted state re-read through the API after the take: 16 references; the
  upload has attribution `Mara Lindqvist` and sits on `Window light`; `Portrait
  studies` holds 2; `Doorway, late light` carries `stone` and the appended note;
  `query=light&boardIds=<Architecture>` returns 3.
- Zero page errors, zero non-localhost requests (all blocked and counted), zero
  API responses ≥ 500. Worker log quiet after the take (a first-attempt reset that
  truncated the worker's singleton dispatch-cursor row was corrected before the
  recorded take; `reset-content.ps1` now resets that row instead).
- Encoded MP4: 142.38 s, 1440 × 1040; the chaptered player loads it, plays, and
  every one of the nine chapter buttons seeks within two seconds of its start.
- Complete video/audio decode without errors. Narration mean −16.3 dB, peak −1.5 dB.
- All nine chapter frames plus eleven mid-action frames were extracted from the
  encoded file and visually reviewed: headings, hover overlays, the decoded draft
  preview, board notices, pressed tag chip, saved tag/note states, search result
  counts and the applied board filter are all legible; header and captions stay
  in their own strips outside the recorded interface.
- Poster: frame extracted from the encoded video at 26.5 s (chapter 2, hover
  overlay visible).

Machine output (`recording.json`, `playback-check.json`, `media-check.json`,
`final-*.jpg`, `review-*.jpg`) is in the ignored `source/` directory.
