# Inspiration UI video

Open `index.html` for the chaptered player, or play `inspiration-tour.mp4` directly. See `comparison.md` for the mock comparison and `transcript.md` for the narration. SRT and WebVTT captions are included, with captions also burned into a separate strip below the interface.

The video follows the critique tour style: 1440 × 1040, 25 fps, H.264/AAC, dark chapter header, highlighted pointer, synthetic Emma Multilingual narration, and a centred 375-pixel mobile capture. The real Angular UI runs with its existing Playwright service bindings. All saves affect only in-memory fixtures. No AI or source-import request is submitted.

## Reproduce

From the repository root, with frontend and e2e dependencies installed:

1. Run `npm --prefix frontend run start:e2e` in one terminal (port 4207).
2. Run `python -m http.server 8765 --directory docs/mocks` in another.
3. Run `node e2e/demo/inspiration/record.mjs --dry` to rehearse and assert the interactions.
4. Install the Python tooling if needed: `python -m pip install edge-tts imageio-ffmpeg`.
5. Run `python e2e/demo/inspiration/narrate.py` (sends only the authored narration to Edge TTS).
6. Run `node e2e/demo/inspiration/record.mjs`.
7. Run `python e2e/demo/inspiration/assemble.py`.
8. Run `node e2e/demo/inspiration/verify-player.mjs`.
9. Run `python e2e/demo/inspiration/verify-media.py` and inspect the generated `source/final-*.jpg` chapter frames.

`APP_URL` and `MOCK_URL` override the default app and mock addresses. Raw recordings, narration audio, calibration frames, and verification output live in ignored `source/`. Cached fixture images and authored scripts live in `e2e/demo/inspiration/`.

## Acceptance checks

- The real UI and reference mock appear in the video using matching photographs.
- Comparison labels and narration distinguish implemented behaviour from mock-only features.
- Browser assertions verify pagination, metadata saving, link-only saving, error retry, and mobile viewport fit.
- No page errors, unexpected external requests, analysis requests, or source-import requests occur.
- The assembled MP4 decodes, plays, seeks through its chapters, and has audible narration plus timed captions.
