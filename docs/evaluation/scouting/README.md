# Scouting report release evaluation

This folder holds the release evaluation for the location scouting report
(`L2-059.5`, `L2-059.6`). It is a procedure, not a passed check: nothing here is
recorded as passing until a named reviewer runs it against the configured
production model and records the outcome.

## What is checked in

- `manifest.json` — the evaluation set: eight fixture slots (an open park, an urban
  street with strong lines, a window-lit interior, a narrow alley, a waterfront, a
  large hall, a playground, and a night-lit scene), each with its acceptable
  observations, prohibited claims, and actionability checks; the gate of 24 runs,
  24 contract passes and at least 22 evidence passes; and the reviewer, model, and
  asset fields marked `<TO SUPPLY>`.
- Deterministic acceptance fixtures live in `backend/tests/Loupe.Api.Tests/Scouting`
  and are not part of this set. They change only through specification review.

## Before the first run

1. Supply two to five licensed or synthetic images per fixture under
   `fixtures/<id>/` and record the licence in the manifest.
2. Write each fixture's brief with no logistics detail (no permits, fees, hours,
   parking, distances, orientation, or place names).
3. Name the reviewer in the manifest.
4. Confirm `Ai:Endpoint`, `Ai:Deployment`, `Ai:ApiKey`, and `Ai:Mode=Live` point at
   the production model under evaluation (`backend/README.md`, Azure OpenAI).

## Procedure

For each fixture, three times:

1. Create a location in a clean library, upload the fixture's images, set the
   brief, and request a scouting report (`POST /api/locations/{id}/scouting-report`).
2. Wait for the operation to reach `Succeeded`; a `Failed` run counts as a failed
   contract check with its safe failure code recorded.
3. Save the returned report JSON under `runs/<release>/<fixture>-<n>.json`.
4. Contract check: the report passed `ScoutingReportValidator` (it would not have
   been published otherwise) and contains none of the fixture's prohibited claims
   nor the common prohibited claims.
5. Evidence check: the reviewer confirms every acceptable observation that the
   images support is reflected or at least not contradicted, that every
   Recommended or Avoid period names a visible basis, and that each actionability
   check holds, recording the reason for every pass and fail in
   `runs/<release>/reasons.md`.

## Gate

The release passes only when all 24 outputs satisfy the contract check and at least
22 satisfy every evidence and actionability check. A failed evaluation, or a change
to the model, deployment, or `ScoutingPrompt`/`ScoutingResponseFormat` that affects
scouting behaviour, fails the quality gate until the changed configuration passes
the same evaluation.

## Recording

Record the deployment name, prompt version, run timestamps, operation identifiers,
outcomes, and reviewer reasons. Never record secrets, private inputs, or raw provider
responses beyond the saved report JSON. An unexecuted evaluation is recorded as not
run, never as passing (`L2-050.5`).
