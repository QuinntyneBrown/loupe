# Find a location relevance evaluation

This folder holds the release evaluation for Meaning search over locations
(`L2-061.9`). It is a procedure, not a passed check: nothing here is recorded as
passing until a named reviewer runs it against the configured production embedding
model and records the outcome. The deterministic acceptance tests in
`backend/tests/Loupe.Api.Tests/ShootPlanning/FindLocationsMeaningTests.cs` use
hand-built vectors and prove ranking, threshold, ties and generation handling only;
they say nothing about the relevance of a real model.

## What is checked in

- `manifest.json` — the evaluation set: 30 corpus location slots (name, setting,
  tags and brief fixed; images, notes and licence `<TO SUPPLY>`), 8 queries with at
  least three pre-labeled relevant locations each — the three example searches shown
  on the Find a location page are among them — the gate, and the reviewer and model
  fields marked `<TO SUPPLY>`.
- `manifest.schema.json` — the shape the manifest must keep.

## Before the first run

1. Supply two to five licensed or synthetic images per corpus location under
   `corpus/<id>/` and record the licence in the manifest.
2. Confirm `Embeddings:Endpoint` points at the Ollama instance beside the API with
   `bge-m3` pulled, and record the pulled digest under `model.versionOrDigest`
   (`backend/README.md`, local embeddings).
3. Confirm `Ai:Endpoint`, `Ai:Deployment`, `Ai:ApiKey` and `Ai:Mode=Live` point at
   the production scouting model: every corpus location must hold a generated
   report before a query runs.
4. Name the reviewer in the manifest and write the label reason for every relevant
   location of every query.

## Procedure

1. In a clean library, create the 30 corpus locations with their images, brief,
   notes and tags, request a scouting report for each, and wait until every
   detail reports `reportStatus: Ready` and `indexStatus: current`
   (`GET /api/locations/{id}`).
2. For each of the 8 queries run
   `GET /api/locations/search?mode=meaning&query=<text>&pageSize=5` with no
   filters, and save the response under `runs/<release>/<query-id>.json`.
3. Relevance check: for each query, count the results among the first five whose
   id is in the query's pre-labeled `relevant` set. The reviewer records, for every
   result, whether it is relevant and why, in `runs/<release>/reasons.md`; a result
   outside the labeled set may be recorded as relevant only with a reason, and that
   reason is reviewed before the run counts.
4. Ownership and filter check: repeat two queries with a shoot-type filter and a
   people count and confirm every result satisfies the filter; repeat one query as
   a second user and confirm no corpus location appears.

## Gate

The release passes only when at least three of the first five results are relevant
for at least six queries and at least two are relevant for every query. A failed
evaluation, or a change to the embedding model, its digest, the 0.20 threshold or
`LocationSearchInputBuilder` (`location-document-v1`), fails the quality gate until
the changed configuration passes the same evaluation.

## Recording

Record the model name and digest, the endpoint kind, the document version, run
timestamps, and the reviewer's reasons. Never record secrets, private inputs, or raw
provider responses beyond the saved search responses. An unexecuted evaluation is
recorded as not run, never as passing (`L2-050.5`).

## Load-profile seed for locations

`L2-047` seeds each of the 50 baseline users with 200 locations holding four images
each, at least 80% of them with a scouting report and a current vector, and draws
each request category proportionally from the Locations endpoints:

- list → `GET /api/locations`
- detail → `GET /api/locations/{id}`
- keyword search with filters → `GET /api/locations/search?query=…&shootTypes=…&people=…`
- writes → `PUT /api/locations/{id}/notes` and `PUT /api/locations/{id}/tags`
- job admission → `POST /api/locations/{id}/scouting-report`

Semantic queries keep their separate budget (`L2-047.2`: 10 users, one Meaning
search every two seconds, a controlled 100 ms embedding response, production
`search_vectors` storage). The seed is generated through the public API so that
every location records its index intent and the worker builds its vector before
the run starts; a seed that bypasses the API does not count. The capacity run has
not been executed for locations and is recorded as not run.
