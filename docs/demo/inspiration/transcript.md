# Inspiration UI tour and mock comparison

This recording uses in-memory reference fixtures in the real Angular interface. No analysis or import requests were made. Narration is synthetic (Microsoft Edge Emma Multilingual).

## 00:00:00 — The Inspiration mock

The Inspiration mock puts the image collection beside a boards sidebar, with tag filters above it. Card overlays show titles and photographer labels. These names and images are demonstration placeholders.

## 00:00:15 — The working Inspiration page

Here is the real Angular interface with the same first twelve images. The heading, navigation, restrained colours, and image grid carry through. This recording uses in-memory fixtures, with no production writes or AI requests.

## 00:00:31 — Mock and implementation side by side

The difference is visible side by side. The mock has boards, counts, and tag filters. The current page uses the full width for references, with saved status and dates in the card captions. Boards, filtering, and on-card board actions are still mock-only.

## 00:00:49 — Browse the saved collection

The collection opens with twenty-four references. Load more brings in the remaining item. Titles open each saved reference, keeping browsing separate from the detailed source information and personal study notes.

## 00:01:04 — Keep the source and your notes

A reference opens into its own detail page. The photograph stays connected to its source, attribution, and notes. Edit metadata lets us record what to study, then save that context back to the fixture library.

## 00:01:20 — Save a source for later

Save link keeps a source URL with an optional title, attribution, and notes. Saving opens a link-only reference. It does not invent a preview or fetch the source. The separate import action is shown here but is not submitted.

## 00:01:37 — A recoverable loading failure

This is a deliberately simulated loading failure. The page gives a clear error and a retry action. Retrying restores the image collection. The demonstration verifies the recovery as well as the successful browsing path.

## 00:01:53 — Inspiration at mobile width

At three hundred and seventy-five pixels, the working collection reflows to fit the screen without horizontal scrolling. The save and upload entry points remain available, and each reference still opens through its title.

## 00:02:08 — What matches, and what remains

The working page delivers collection browsing, reference details, notes, and saving source links. The mock goes further with boards and tag filtering. This comparison preserves those visible gaps, so the video demonstrates the current product honestly.

Reference: `docs/mocks/inspiration.html`. Images are public Picsum placeholders cached in `e2e/demo/inspiration/images`. Titles and photographer names come from the mock and are fictional labels, not verified credits.
