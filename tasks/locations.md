# Locations, scouting reports, and shoot planning

Branch: `feat/locations`. Scope is L1-014, L1-015, and L1-016 — L2-055 through
L2-062 plus the shared sections they extend (L2-031/032 deletion, L2-033..036
processing, L2-041 disclosure, L2-043..045 for the fifth area, L2-028 indexing).
`docs/detailed-designs/{locations,scouting,shoot-planning}/` name the components;
`docs/mocks/{locations,location,find-location}.html` fix the screens. Meaning mode
is delivered on the recorded decisions in `plan.md`: pgvector in PostgreSQL and
Ollama `bge-m3` as the in-deployment embedding service. Complete one acceptance
slice at a time: Given–When–Then (the numbered criteria in `todo.md`), expected
RED, implementation, GREEN, regressions, commit.

## Delivery checklist

- [x] A1a Create a location and read it back (L2-055.1)
- [x] A1b Validate location details at their limits (L2-055.3, .7)
- [ ] A2 Edit details, brief, notes, and tags with revision protection (L2-055.2, .4, .5, .6)
- [ ] A3 List owned locations with cursor paging (L2-057.1, .4)
- [ ] A4 Add, remove, and choose the cover of location images (L2-056.1–.8)
- [ ] A5 Delete a location and complete its cleanup (L2-057.6, .7; L2-031.5, .7; L2-032.2)
- [ ] A6a Browse the Locations area (L2-057.1, .4; L2-043.1, .2, .5; L2-044.1; L2-045.1, .6)
- [ ] A6b Add a location from the grid (L2-055.1, .3, .8; L2-043.7; L2-044.6; L2-045.2)
- [ ] A7 Inspect a location and edit its details (L2-055.2, .4, .5, .6, .8; L2-057.2, .3, .4, .6; L2-044.2)
- [ ] A8 Manage location images in the gallery (L2-056.1–.6, .9; L2-057.2, .5)
- [ ] B1 Admit a scouting report request (L2-060.1, .2, .5, .7; L2-033.1, .5; L2-035)
- [ ] B2 Generate, validate, and publish a scouting report (L2-058.1–.7; L2-059.3, .4; L2-060.4; L2-034.1–.3; L2-036.6)
- [ ] B3 Outdate, cancel, and retry scouting work (L2-060.3, .6; L2-056.5; L2-031.7; L2-033.4; L2-034.4)
- [ ] B4 Request and read the scouting report in the detail (L2-060.1–.8; L2-058.1; L2-057.3; L2-041.2)
- [ ] B5 Release evaluation manifest and procedure (L2-059.5, .6; L2-050.2, .5)
- [ ] C1 Find locations by keyword with shoot filters (L2-062.1, .2, .3, .7, .8; L2-061.2–.6)
- [ ] C2 Find a location page with keyword results (L2-061.1, .5, .6; L2-062.8; L2-043.2; L2-044.1)
- [ ] C3a Record location index intents and report index status (L2-028.2; L2-062.4, .5)
- [ ] C3b Embed location documents through Ollama and keep vectors current (L2-028.1, .4, .5; L2-062.4, .6, .7; L2-041)
- [ ] C4 Rank locations by meaning (L2-061.1, .4, .7, .8; L2-062.5, .7)
- [ ] C5 Meaning mode in the Find a location page (L2-061.1, .6, .7, .8; L2-062.4–.6)
- [ ] C6 Relevance evaluation corpus and procedure (L2-061.9; L2-047)
- [ ] D Design-system examples, README, final evidence, PR

## Evidence

Record commands and observed RED/GREEN results here as slices complete. Backend
checks run in the acceptance container (`./backend/Test.ps1 -Filter …`); browser
checks run in Chromium only (`cd e2e && npx playwright test specs/<name>.spec.js`).
Azure OpenAI and Ollama were not configured in the planning shell; live checks are
recorded only when actually executed.

### A1a — Create a location and read it back (L2-055.1)

- Test: `backend/tests/Loupe.Api.Tests/Locations/CreateLocationTests.cs`
  (`L2_055_1_A_named_location_persists_with_absent_address_images_and_report`):
  name-only `POST /api/locations` with an `Idempotency-Key` → 201 with a
  `Location` header; the result and a later `GET` after an API restart show the
  trimmed name with every address field, `coordinates`, `setting`, `scoutingBrief`,
  `notes`, `coverImageId` and `report` null, `tags` and `images` empty,
  `reportStatus` `None`, `revision` 1 and `createdAt == updatedAt`; replaying the
  same key returns the same id; a stranger's `GET` → 404; `/api/photographs`,
  `/api/references`, `/api/photographers` and `/api/search?query=Kew` stay empty.
- RED: `./backend/Test.ps1 -Filter 'FullyQualifiedName~CreateLocationTests'` →
  `Failed: 1` — `Assert.Equal() Failure: Expected: Created / Actual: NotFound`
  at the first save (no `api/locations` route existed).
- Built: `Loupe.Domain/Locations/{Location,LocationTag,LocationSetting}`;
  `Loupe.Application/Locations/{ILocationStore,CreateLocationCommand[Handler],
  GetLocationQuery[Handler],LocationDetails,LocationDetailsValidator,LocationResult,
  LocationTagInput,LocationTagResult,LocationImageResult,Coordinates}` (receipt type
  `location`, `TextField`/`TagName` normalization, ≤ 50 distinct tags);
  `LibraryDbContext` `locations`/`location_tags` (alternate key `(Id, OwnerId)`,
  `Revision` default 1 concurrency token, index `(OwnerId, CreatedAt, Id)`,
  `Setting` stored as text, `numeric(9,6)`/`numeric(10,6)` coordinates); migration
  `Locations`; `LocationStore`; `LocationsController` `POST` + `GET {id}`;
  `CreateLocationRequest`.
- GREEN: same filter → `Passed: 1`. Band
  `-Filter 'FullyQualifiedName~Locations|FullyQualifiedName~Search'` → `Passed: 49`.
  `dotnet build backend/Loupe.slnx` → 0 warnings, 0 errors.
- Non-claims: `dotnet format --verify-no-changes` reports whitespace findings only
  in files this slice did not touch (Boards, Photographers, Search tests, stores) —
  pre-existing on `main`, left alone. Coordinates, `setting` and field limits are
  accepted by the schema but not yet validated or bound from the request (A1b);
  `images`, `coverImageId`, `report` and `reportStatus` are constants until A4/B1.

### A1b — Validate location details at their limits (L2-055.3, .7)

- Tests (`CreateLocationTests`, 23 new cases): every text field at its maximum
  (200/200/200/100/100/20/100/2,000/10,000 runes, using `é`) round-trips and one
  rune more → 400 `invalid_request` naming the field; `90.000000`/`-90`/
  `±180.000000` coordinates persist and read back with six places; `90.000001`,
  `-90.000001`, `±180.000001`, a seventh decimal place, `north`, and a lone
  latitude or longitude name the offending field (the missing half of a pair);
  `Indoor`/`Outdoor`/`Mixed` persist, blank becomes absent, `Underwater` → field
  error `setting`; script/markup, SQL, and template text in name, address line and
  locality are returned byte-for-byte and the library keeps accepting saves.
- RED: `-Filter 'FullyQualifiedName~CreateLocationTests'` → `Failed: 13, Passed: 11`
  — every coordinate and setting case returned `Created` instead of `BadRequest`
  (or `coordinates`/`setting` came back null) because the request ignored those
  fields; the 11 text-limit and hostile-text cases already passed on A1a's
  `TextField` normalization, so they confirm rather than drive that behaviour.
- Built: `CoordinatesInput`; `CreateLocationCommand`/`CreateLocationRequest` carry
  `coordinates` and `setting`; `LocationDetailsValidator` parses invariant decimal
  degrees (sign + point only, scale ≤ 6, |lat| ≤ 90, |lon| ≤ 180, pair required)
  and the `LocationSetting` name case-insensitively; `LocationDetails` carries them.
- GREEN: band `-Filter 'FullyQualifiedName~Locations|FullyQualifiedName~Search'` →
  `Passed: 72`. `dotnet build` clean; `dotnet format` reports nothing under
  `Locations`.
- Non-claims: "previously saved values remain unchanged" is proven for edits in A2
  (an invalid `PUT` leaves the record identical); "displayed" and "searched" for
  L2-055.7 land with the detail page (A7) and keyword search (C1).

