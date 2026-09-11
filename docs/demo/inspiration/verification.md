# Recording verification

Verified on 2026-09-11 using Chromium and FFmpeg.

- Duration: 146.58 seconds; 1440 × 1040; nine chapters.
- Browser rehearsal and timed recording passed assertions for 24-to-25 item pagination, metadata save, link-only save, simulated failure and retry, and 375-pixel viewport fit.
- Zero browser page errors, unexpected external requests, analysis requests, and source-import requests.
- MP4 played successfully; all nine chapter buttons sought to their expected timestamps.
- Complete video/audio decode passed without errors. Mean audio level: -16.2 dB; peak: -1.5 dB.
- All nine rendered chapter frames were visually reviewed, including full-size comparison and mobile frames. Headers and captions occupy separate strips outside the recorded UI.
- The comparison documents the missing boards, counts, tag filters, card actions, and navigation destinations. It does not claim mock parity.

The recording used a separate Angular e2e server at `http://localhost:4217` because the existing port-4207 server returned HTTP 500 for `main.js`. The mock was served at port 8765. Only fixture bindings were used for application data. Detailed machine output and review frames are in ignored `source/`; reproduction commands are in `README.md`.
