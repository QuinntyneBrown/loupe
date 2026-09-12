# Inspiration library and keyword search

This recording drives the real Angular application against the real Loupe.Api, Loupe.Worker and PostgreSQL stack from `docs/demo/harness`. Every save, board, tag, note and search result shown is real persisted state; no AI provider is configured and no AI request is made. Narration is synthetic (Microsoft Edge Emma Multilingual).

## 00:00:00 — A private library, running for real

Loupe is a private library for the photographs that inspire you. This recording drives the real Angular application against the real API, worker, and database, signing in with a locally provisioned account.

## 00:00:14 — Browse the Inspiration library

Inspiration opens on the whole collection: fifteen references across three boards. Boards and their counts sit in the sidebar, the most used tags line up above the grid, and hovering a card reveals its title, photographer, and a link to the original source.

## 00:00:32 — Save a reference by uploading an image

Saving starts from one button. Upload an image and Loupe reads it, shows a preview, and asks for a title and photographer. Tick a board, save, and the reference appears at the front of the library with the board count updated.

## 00:00:48 — Organise references into boards

Boards organise the library. Create a new board, then add references to it straight from their cards. The sidebar count follows each change, and opening the board shows only what belongs there.

## 00:01:02 — Filter the collection by tag

Tags work as filters. Choosing window light narrows the collection to the references carrying that tag, and the chip stays pressed until it is cleared. Clearing the tag restores the full library.

## 00:01:15 — Tags and notes on a reference

Every reference has its own page: the full image, photographer, boards, source and attribution, tags and notes. Adding a tag saves straight away, and notes save with one click, so the context travels with the photograph.

## 00:01:32 — Keyword search across the library

Search covers everything you have saved. Keyword search matches the words you typed, across titles, notes, tags, attribution, and photographers. Searching for a photographer's name returns the photographer alongside the references that credit them.

## 00:01:50 — Narrow results by board

Results narrow by type, boards, and tags. Searching for light and filtering to the Architecture board leaves only the architecture references that mention light. Opening a result lands on the reference page, with the tag added a moment ago.

## 00:02:07 — Saved for real

Everything shown was saved for real: the upload, the board, the tags and the note are in the database, and search found them a moment later. That is the Inspiration library today: browse, save, organise, and find.

Images are public Picsum placeholder photographs cached in `e2e/demo/inspiration/images` (original URLs in `images.json`). Titles, notes, tags, boards and photographer names are fictional demonstration labels from `library.json`, not verified credits.
