# Critique page demo

Open `index.html` for the chaptered player, or play `critique-tour.mp4` directly.

- Duration: 2 minutes 54 seconds; 1440 × 1040, 25 fps, H.264 video and AAC audio.
- Narrated with a synthetic Microsoft Edge Emma Multilingual voice.
- Captions are burned into a separate strip below the interface. Editable SRT, WebVTT and a transcript are included.
- The opening shows `docs/mocks/critique.html`, followed by the real Angular page using the same shoreline photograph.
- All critiques and saved data are fabricated through the existing Playwright service bindings. No critique-provider requests or live user-data writes occur. Production Demo execution remains removed.
- The second attempt reuses the same fixture photograph; it is not a genuine reshoot.

The tour covers mock alignment, evidence crops and spotlights, click-to-pin, Escape and keyboard focus, technical and composition feedback, three improvements, practice exercise, note saving, regeneration confirmation/cancellation, comparison selection, and mobile reading order without horizontal overflow. It does not demonstrate a successful real-provider generation.

## Reproduce

From the repository root, with the existing npm dependencies and Chromium installed:

1. Start the fixture application using `npm run start:e2e` from `frontend/` (port 4207).
2. Serve `docs/mocks/` on port 8765: `python -m http.server 8765 --bind 127.0.0.1 --directory docs/mocks`.
3. Install narration/render dependencies: `python -m pip install edge-tts imageio-ffmpeg`.
4. Generate narration: `python e2e/demo/critique/narrate.py`. This sends the authored script to the online Edge speech service; it needs internet access but no API key.
5. Rehearse: `node e2e/demo/critique/record.mjs --dry`.
6. Record: `node e2e/demo/critique/record.mjs`.
7. Assemble: `python e2e/demo/critique/assemble.py`.
8. Check player playback and chapter seeking: `node e2e/demo/critique/verify-player.mjs`.

The image is bundled at `e2e/demo/critique/reference-photo.jpg` and comes from the mock's public placeholder URL, https://picsum.photos/seed/lp-s3/1200/800. Raw recordings, audio segments and verification outputs are retained under the ignored `source/` folder.

## Verification performed

- Chromium recording assertions passed for evidence selection, keyboard clearing, note persistence, regeneration cancellation, comparison selection and 375 px viewport overflow.
- Zero page errors, unexpected network requests or critique requests in the recording.
- Entire final MP4 decoded successfully. Audio mean volume: −16.4 dB; peak: −1.5 dB.
- Rendered frames inspected across all twelve chapters; captions occupy their own strip and do not obscure the interface.
- Chromium player loaded the 173.74-second video, advanced playback and successfully sought to three chapters.
