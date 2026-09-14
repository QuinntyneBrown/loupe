# Locations, scouting reports and Find a location

This recording drives the real Angular application against the real Loupe.Api, Loupe.Worker and PostgreSQL (pgvector) stack from `docs/demo/harness`, with a local Ollama `bge-m3` model embedding every location for Meaning search. Every location, image, note, tag, index update and search result shown is real persisted state. No Azure OpenAI credentials are configured, so the scouting report request is shown being refused, and the finished report is shown as a synthetic example on the design-system site. Narration is synthetic (Microsoft Edge Emma Multilingual).

## 00:00:00 — Places, seen before the shoot

Loupe now keeps the places you shoot as well as the photographs. This recording drives the real Angular application against the real API, worker, and database, with a local embedding model running beside them. It starts by signing in with a locally provisioned account.

## 00:00:18 — Browse the Locations library

Locations opens on the whole library: seven places, each a card with its cover, name, locality, and image count. Hovering a card reveals those details, and every card carries its report status; none of these has a scouting report yet. Find a location sits in the header.

## 00:00:38 — Add a location

Adding a location is one dialog: a name, an address you type yourself, never read from a photo, a setting, a scouting brief, private notes, and tags. Save, and the new card appears at the front of the library with a placeholder until it has images.

## 00:00:56 — Upload images and choose a cover

A location holds up to ten images. Choose several at once and each one uploads on its own with its own progress. The gallery shows the full frame, the thumbnails are a radio group, so the arrow keys move the selection, and any image can become the cover.

## 00:01:14 — Notes, tags, and a search index that keeps up

The brief, notes, and tags save inline, each with its own status. Every save records an index intent: the status reads Updating search while the worker embeds the location through the local model, and clears the moment the vector is current.

## 00:01:31 — Ask for a scouting report

A scouting report is requested explicitly, and the disclosure says exactly what leaves the server: the images, the brief, and camera settings; never the address, coordinates, notes, or tags. This stack has no Azure OpenAI credentials, so the request is refused with a clear message, and editing continues.

## 00:01:53 — What a scouting report looks like

What a finished report looks like is on the design-system site, with synthetic content. Six sections in a fixed order: overview, suitability by shoot type, times of day, techniques, group size, and cautions. Ratings are words, never scores, every entry names its evidence basis, and citing an image selects it in the gallery.

## 00:02:16 — Find a location by keyword

Find a location searches your own library. Keyword mode matches every word you type against a location's details and, once it has one, its report. Setting and tag filters work on any location, including the one added a moment ago, while shoot type, people, and time of day read the report.

## 00:02:37 — Find a location by meaning

Meaning mode ranks by what the locations describe, not by matching words. The query is embedded by the local model and compared with each location's current vector, and the results carry the Matched by meaning label. A calm waterside shoot at first light surfaces the water locations first, without those words in their tags.

## 00:02:58 — Saved for real

Everything shown was saved for real: the location, its images and cover, the notes, the tags, and every search index update are in the database, and both searches found them a moment later. That is the Locations area today: keep the places, ask for the report, and find the right one for the shoot.

Images are public Picsum placeholder photographs cached in `e2e/demo/inspiration/images`. Location names, addresses, briefs, notes and tags are fictional demonstration labels from `e2e/demo/locations/library.json`, not real scouting advice.
