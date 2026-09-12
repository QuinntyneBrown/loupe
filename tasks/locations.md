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
- [x] A2 Edit details, brief, notes, and tags with revision protection (L2-055.2, .4, .5, .6)
- [x] A3 List owned locations with cursor paging (L2-057.1, .4)
- [x] A4 Add, remove, and choose the cover of location images (L2-056.1–.8)
- [x] A5 Delete a location and complete its cleanup (L2-057.6, .7; L2-031.5, .7; L2-032.2)
- [x] A6a Browse the Locations area (L2-057.1, .4; L2-043.1, .2; L2-044.1; L2-045.1, .6)
- [x] A6b Add a location from the grid (L2-055.1, .3, .8; L2-043.7; L2-044.6; L2-045.2)
- [x] A7 Inspect a location and edit its details (L2-055.2, .4, .5, .6, .8; L2-057.2, .3, .4, .6; L2-044.2)
- [x] A8 Manage location images in the gallery (L2-056.1–.6, .9; L2-057.2, .5)
- [x] B1 Admit a scouting report request (L2-060.1, .2, .7; L2-033.1; L2-035)
- [x] B2 Generate, validate, and publish a scouting report (L2-058.1–.7; L2-059.3, .4; L2-060.4, .5, .6, .8; L2-034)
- [x] B3 Outdate, cancel, and retry scouting work (L2-060.3, .6; L2-056.5; L2-055.5; L2-031.7; L2-034.4)
- [x] B4 Request and read the scouting report in the detail (L2-060.1–.8; L2-058.1, .7; L2-057.3; L2-055.5; L2-041.2; L2-045.3)
- [x] B5 Release evaluation manifest and procedure (L2-059.5, .6; L2-050.2, .5 — procedure only)
- [x] C1 Find locations by keyword with shoot filters (L2-062.1, .2, .3, .5, .7; L2-061.2–.5)
- [x] C2 Find a location page with keyword results (L2-061.1, .5, .6; L2-062.8; L2-043.2; L2-044.1)
- [x] C3a Record location index intents and report index status (L2-028.2; L2-062.4, .5)
- [x] C3b Embed location documents through Ollama and keep vectors current (L2-028.1, .4, .5; L2-062.4, .6, .7; L2-041)
- [x] C4 Rank locations by meaning (L2-061.1, .4, .7, .8; L2-062.5, .7)
- [x] C5 Meaning mode in the Find a location page (L2-061.1, .6, .7, .8; L2-062.4–.6)
- [x] C6 Relevance evaluation corpus and procedure (L2-061.9; L2-047)
- [x] D Design-system examples, README, final evidence, PR

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

### A2 — Edit details, brief, notes, and tags with revision protection (L2-055.2, .3, .4, .6)

- Test: `backend/tests/Loupe.Api.Tests/Locations/UpdateLocationTests.cs` (11 cases):
  `PUT {id}` with every optional field, then `PUT {id}/scouting-brief`, `/notes`,
  `/tags` → the reload shows trimmed values, a blank second address line as absent,
  `51.487210`/`-0.287600`, `Outdoor` from `outdoor`, CRLF-normalized notes, tags
  sorted with their categories, revision 5 and `updatedAt` after `createdAt`
  (test clock advanced); details and notes edits leave the tags byte-identical;
  an empty name, a 201-rune address line, `Underwater`, or latitude 91 → 400
  naming the field and the record reads back identical; brief 2,000 / notes
  10,000 accepted and one more rejected with the record unchanged; 51 tags → 400
  `tags`; after one editor wins at revision 1, every stale edit route → 409
  `revision_conflict` with the record unchanged and revision 2 succeeds; a
  stranger → 404 on every edit route.
- RED: `-Filter 'FullyQualifiedName~UpdateLocationTests'` → `Failed: 11, Passed: 0`
  — `PUT /api/locations/{id}` returned `MethodNotAllowed`, the text and tag routes
  `NotFound`. One later failure was the test's own: `updatedAt > createdAt` under
  the frozen `TestClock`; fixed by advancing the clock, not by relaxing the assert.
- Built: `LocationDetailsInput`, `LocationTextField`, `UpdateLocationCommand[Handler]`,
  `UpdateLocationTextCommand[Handler]`, `SetLocationTagsCommand[Handler]`;
  `LocationDetailsValidator` now exposes `Normalize(input)`, `Text(field, text)`
  and `Tags(tags)` shared by create and edit; `ILocationStore.UpdateAsync/
  UpdateTextAsync/ReplaceTagsAsync` in `LocationStore` (owner-scoped load, revision
  check, `UpdatedAt` from `TimeProvider`, `Revision++`, `DbUpdateConcurrencyException`
  → 409, diff-merge of tags); `PUT {id}`, `PUT {id}/scouting-brief`, `PUT {id}/notes`,
  `PUT {id}/tags` with `UpdateLocationRequest`, `UpdateLocationTextRequest`,
  `SetLocationTagsRequest`.
- GREEN: band `-Filter 'FullyQualifiedName~Locations|FullyQualifiedName~Search'` →
  `Passed: 83`. `dotnet build` clean; `dotnet format` reports nothing under `Locations`.
- Non-claims: L2-055.4's images, cover, report and report status are proven
  unchanged once they exist (A4, B3); L2-055.5 (brief edit keeps the report current
  and offers Regenerate) lands with B3; the UI halves of .2/.4/.6 land in A7.

### A3 — List owned locations with cursor paging (L2-057.1, .4 — API half)

- Test: `backend/tests/Loupe.Api.Tests/Locations/BrowseLocationsTests.cs` (6 cases):
  25 owned locations plus a stranger's location, a reference and a photograph →
  `GET /api/locations?pageSize=10` walks three pages (10/10/5, `nextCursor` null on
  the last) in creation-date-descending then id order with every location once,
  `totalCount` 25, each item carrying name, locality-or-null, `coverPreviewUrl`
  null, `imageCount` 0 and `reportStatus` `None`; the default page is 24;
  `/api/photographs` and `/api/references` still hold one item each and the
  stranger sees only theirs; an empty library is a 200 empty page with
  `totalCount` 0; `pageSize=0`/`101`/`cursor=invalid` → 400 naming the field; a
  cursor replayed with another page size or by another owner → 400 `cursor`.
- RED: `-Filter 'FullyQualifiedName~BrowseLocationsTests'` → `Failed: 6, Passed: 0`
  — `GET /api/locations` returned `MethodNotAllowed`.
- Built: `LocationSummary`, `LocationPage`, `ListLocationsQuery[Handler]`
  (1–100 page size, scope-bound cursor), `LocationListCursor` (owner + page size
  scope over `CreatedCursor`), `ILocationStore.ListAsync/CountAsync` in
  `LocationStore` (keyset paging on `(CreatedAt desc, Id)`), `GET /api/locations`
  with `pageSize` (default 24) and `cursor`.
- GREEN: band `-Filter 'FullyQualifiedName~Locations|FullyQualifiedName~Search'` →
  `Passed: 89`. `dotnet build` clean; `dotnet format` reports nothing under `Locations`.
- Non-claims: `coverPreviewUrl` and `imageCount` are constants until images exist
  (A4 replaces them with the cover image's preview and the real count); the grid,
  placeholder, empty state and failure recovery are A6a.

### A4 — Add, remove, and choose the cover of location images (L2-056.1, .2, .3, .5, .6, .7, .8)

- Tests: `backend/tests/Loupe.Api.Tests/Locations/LocationImagesTests.cs` (7 cases)
  with `LocationFixture` (create, `SubmitImageAsync`, `AddImageAsync`): PNG, JPEG,
  WebP, HEIC and an orientation-6 JPEG become five images in upload order with
  positions 1–5, the first as cover, full PNG and JPEG preview at the oriented
  size (10×20 for the rotated file) after an API restart, and the grid card shows
  `imageCount` 5 with the cover's preview URL; the eleventh upload → 400 naming
  `images` with the media folder unchanged and ten images kept; a replayed
  `Idempotency-Key` adds nothing, an undecodable file → 415 with no new bytes and
  the later upload still succeeds; removing the middle of three keeps `[a, c]` at
  positions 1–2 with the cover on `a`, the removed URL → 404, removing the cover
  moves it to `c`, the card follows, and the cleanup worker deletes four files while
  `c` stays readable; a chosen cover survives a restart with the order unchanged
  and a stale revision → 409; a stranger's upload, cover, remove, image and preview
  reads → 404, an unknown location or image id → 404, no file written and the
  record identical; GPS, serial and owner EXIF are absent from the response and
  from both retained copies, while typed coordinates stay exact and untyped ones
  stay absent.
- RED: `-Filter 'FullyQualifiedName~LocationImagesTests'` → `Failed: 7, Passed: 0`
  — `POST /api/locations/{id}/images` returned 404 `request_failed`. A second
  RED after wiring (`unexpected_failure` 500) was diagnosed as EF treating the
  client-keyed `LocationImage` reached through the navigation as an existing row
  (`Modified`, 0 rows affected); fixed by adding the entity explicitly.
- Built: `LocationImage` (`location_images`, position index, JSON `Exif`),
  `Location.CoverImageId`/`ImageSetRevision` (migration `LocationImages`),
  `ILocationImageStore`/`LocationImageStore` (row lock `FOR UPDATE`, cap re-check,
  next position, first-is-cover, position renumbering and cover hand-off on
  remove, `journal.deletions` row `location-image` with both keys, `Revision` and
  `ImageSetRevision` bumps), `AddLocationImageCommand[Handler]` (single file,
  key, ownership and cap before reading bytes, receipt type `location-image`,
  staged keys deleted on refusal), `RemoveLocationImageCommand[Handler]`,
  `SetLocationCoverCommand[Handler]`, `GetLocationImageQuery[Handler]`,
  `LocationImageLimit`, `LocationImageUrls`; `LocationResult.images/coverImageId`
  and `LocationSummary.coverPreviewUrl/imageCount` from real data; routes
  `POST {id}/images`, `DELETE {id}/images/{imageId}?revision`, `PUT {id}/cover`,
  `GET {id}/images/{imageId}[/preview]`; `AbandonedMediaCleaner` and
  `DeletedContentCleaner` treat `location_images` keys as live.
- GREEN: same filter → `Passed: 7`. Band
  `Locations|Search|AbandonedMedia|Cleanup|DeleteReference|DeletePhotograph` →
  `Passed: 114`. `dotnet build` clean; `dotnet format` applied to two new files,
  then clean under `Locations`.
- Non-claims: the L2-056.5 report-outdating half and L2-056.7's deleted-location
  case land with B3 and A5; criteria .3 (per-file queue UI), .4 and .9 are A8.

### A5 — Delete a location and complete its cleanup (L2-057.6, .7; L2-031.5, .7; L2-032.2; L2-056.7)

- Tests: `backend/tests/Loupe.Api.Tests/Locations/DeleteLocationTests.cs` (3 cases):
  deleting a two-image location with notes and tags → 200 `Pending` deletion
  naming the location; detail, both images and both previews → 404 at once; the
  grid holds only the kept location with `totalCount` 1; the cleanup worker
  (`CleanupProcess`) completes the deletion and the four doomed files are gone
  while the kept location's image, the reference and the photograph stay readable;
  a replay after an API restart returns the same deletion id and the kept location
  is intact. A stranger's, an unknown id's, a stale and a zero revision → 404/404/
  409/400 with the record unchanged. A deleted location's upload, cover and
  remove routes → 404 and no file is written (L2-056.7's deleted half).
- RED: `-Filter 'FullyQualifiedName~DeleteLocationTests'` → `Failed: 3, Passed: 0`
  — `DELETE /api/locations/{id}` returned `MethodNotAllowed`. Two later failures
  were the test's own: it asserted the deletion `Completed` before the journal row
  committed, and counted every file in the shared media folder (a sibling case's
  pending deletion removed two more); it now polls the deletion status and tracks
  the doomed files by path.
- Built: `DeleteLocationCommand[Handler]`, `IDeletionStore.DeleteLocationAsync` in
  `DeletionStore` (previous-deletion replay, row lock `FOR UPDATE`, revision check,
  `journal.deletions` row `location` with every image and preview key, cascade
  removal of images and tags, transient Npgsql → 503), `DELETE /api/locations/{id}?revision`.
- GREEN: band `Locations|Search|Cleanup|Deletion` → `Passed: 128`. `dotnet build`
  clean; `dotnet format` clean under `Locations`/`DeletionStore`.
- Non-claims: cancelling an active scouting job on deletion (L2-031.7's job
  clause) and removing the location's search vector land with B3 and C3b; the
  confirmation dialog and immediate UI revocation are A7.

### A6a — Browse the Locations area (L2-057.1, .4 UI; L2-043.1, .2; L2-044.1; L2-045.1, .6)

- Spec: `e2e/specs/locations.spec.js` (14 cases) with page object
  `e2e/page-objects/locations-page.js` and fixture `e2e/fixtures/location-library.js`
  (`window.loupeLocations`): the Library navigation has five links and Locations
  opens by click and by URL/reload with title `Locations · Loupe`, `aria-current`
  and the H1; 25 locations show eight tile skeletons while the list is held, then
  `25 locations`, 24 cards in fixture order with cover or the `No images yet`
  placeholder, name, `Richmond · 1 image · No scouting report` style meta and a
  `Report` pill, Load more → 25 with focus on card 25 and exactly two list calls;
  a failed first load shows the alert with Try again and recovers, a failed Load
  more keeps the 24 cards, an emptied library shows the empty state with two Add
  location controls and passes axe; a failed empty library recovers with focus on
  the empty heading; grid columns 1/2/3/4/5 at 575/576/767/768/991/992/1199/1200;
  axe A/AA and ≥ 24 px targets at 320 and 1440.
- RED: `cd e2e && npx playwright test specs/locations.spec.js` → 14 failed —
  `NG04002: Cannot match any routes. URL Segment: 'locations'` and the navigation
  had 4 links. Two later failures were the spec's own (the mock session is
  in-memory, so a reload needs the sign-in step other specs use); fixed in the
  page object's `reload()`.
- Built: `api` `location-result.ts` shapes, `ILocationService.list` +
  `LOCATION_SERVICE`, `LocationService`; `components` `LocationCard` (cover or
  placeholder, status pill, meta line); `domain` `LocationCollection` (skeletons,
  paging, refresh, retry focus, empty and failed states, `--lp-image-columns`
  grid); `loupe` `LocationsPage`, route `locations`, fifth nav link with `i-pin`
  sprite, tab bar `repeat(5, 1fr)`, `.lp-status--outdated`; `MockLocationService`
  and both `app.providers.ts`; `search-page.js` expects five links.
- GREEN: `specs/locations.spec.js` → 14 passed; band `locations`,
  `search-navigation`, `sign-in`, `photographers` → 36 passed; `npm run build`
  clean apart from the pre-existing 500 kB budget warning; prettier applied to the
  three touched templates.
- Non-claims: the Add location dialog (L2-043.5/.7, L2-055.8) is A6b; the grid's
  Delete action and the detail page are A7.

### A6b — Add a location from the grid (L2-055.1, .3, .8 UI; L2-043.2, .7; L2-044.6; L2-045.2)

- Spec: `e2e/specs/locations-add.spec.js` (12 cases) through the `LocationsPage`
  page object and the fixture's `create` operation (receipted by `operationKey`,
  server-style validation, `errors.create` queue): a name-only save from the header
  closes the dialog, shows `“Kew Bridge foreshore” saved.`, prepends the card and
  sends null coordinates/setting, empty tags and a visible-ASCII key; the empty
  state's Add location saves the full form (address, locality, coordinates,
  setting, brief, notes, a tag) and the card replaces the empty state; a blank name
  → `Enter a name.` at the focused Name field with no create call and Locality
  kept, latitude alone → the longitude error with the latitude kept, a server
  `invalid_request` on `name` → shown at Name with every value kept, a transport
  failure → alert + Try again → saved; Escape, backdrop, Close, Cancel and browser
  Back on a dirty form → Discard unsaved changes? with Keep editing preserving the
  draft (Back keeps the URL and dialog) and Discard closing (or leaving) with a
  fresh empty dialog afterwards; a clean dialog focuses Name, contains Tab focus,
  closes without a prompt and returns focus to the empty-state or header trigger,
  with axe clean; the dialog is a full-width bottom sheet ≤ 90dvh at 375×667 and
  844×390 and a centred ≤ 560 px panel at 1440×900 with Name and Save location
  reachable.
- RED: `cd e2e && npx playwright test specs/locations-add.spec.js` → 12 failed
  (no `Add location` dialog). Later failures were the spec's own: optional-field
  labels carry an "optional" suffix (page object matches `^Label( optional…)?$`),
  singular `1 location`, and in-app navigation is blocked by the native modal, so
  the navigation case uses browser Back as the upload dialog spec does. One
  implementation fix: the invalid field could not take focus while `NgModel`
  re-enabled it in a microtask, so the form focuses after that microtask.
- Built: `api` `LocationInput`/`LocationTagInput`/`LocationDetailsInput`,
  `ILocationService.create`, `LocationService.create` (POST with
  `Idempotency-Key` and CSRF token, field errors parsed into `ServiceError`);
  `domain` `LocationForm` (all fields, tag chips, client validation mirroring the
  server limits and coordinate rules, server error mapping, first-invalid focus,
  `dirty()`/`value()`/`reset()`); `loupe` `AddLocation` dialog (native modal,
  backdrop/Escape/Close → `UnsavedChanges`, focus trap, retry, operation key),
  `LocationsPage` open/added/close with notice and trigger focus restore,
  `LocationCollection.add/focusAdd`, `locationsUnsavedGuard` on the route;
  `MockLocationService.create`.
- GREEN: `specs/locations-add.spec.js` → 12 passed; band `locations-add`,
  `locations`, `search-navigation`, `photographers`, `add-photographer` → 50
  passed; `npm run build` clean apart from the pre-existing budget warning;
  prettier clean.
- Non-claims: the Edit location dialog reuse of `LocationForm` and the detail
  page land in A7; L2-045.2's "next relevant control if the trigger was deleted"
  is exercised by Delete in A7.

### A7 — Inspect a location and edit its details (L2-055.2, .4, .5, .6, .8 UI; L2-057.2, .3, .4, .6 UI; L2-044.2; L2-045)

- Spec: `e2e/specs/location-detail.spec.js` (11 cases) with page object
  `e2e/page-objects/location-page.js`, the fixture's `get`, `update`,
  `updateText`, `setTags` and `deleteLocation` operations (revision-checked,
  `deletions` journal) and the grid page object's `deleteCard`: a location opened
  by URL shows the H1 and `Location · Loupe` title, the address line by line with
  empty lines omitted, six-place coordinates, `12 Sep 2026` created/updated,
  locality and region, the setting pill, brief, notes, tags, `No scouting report
  yet`, three radio thumbnails with `Image 1, cover` checked and `Image 1 · Cover`,
  an `object-fit: contain` stage that follows the selected radio, `3 of 10 images`,
  and survives reload with axe clean; no images → the placeholder with Add images,
  `Address not recorded`, `Not recorded` coordinates and the "Add an image to get a
  scouting report" explanation; an unknown id or a failed load → `Couldn't load this
  location.` with Try again (recovers) and Back to Locations (navigates); Edit
  location pre-fills name, address and coordinates without brief, notes or tags,
  saves normalized values (blank second line dropped, `51.487210, -0.287600`,
  `Mixed`, updated date) leaving the tags untouched and returning focus to More
  actions, a stale revision → conflict notice + disabled save with the attempted
  values kept, Reload latest then save succeeds, latitude 91 → field error; a
  dirty Edit dialog prompts on Escape with Keep editing/Discard; brief and notes
  show Saved → Unsaved → Saved, a failed notes save shows Couldn't save with the
  text kept and retries, tags add/remove → Unsaved → Save → Saved with the brief
  untouched and the fixture holding exactly the saved values; Delete from the
  menu names the location with `Its 3 images, scouting report, notes, tags, and
  search record are all removed.`, Cancel changes nothing and refocuses More
  actions, confirm → `/locations` with `Location deleted.` and one card fewer;
  Delete from a grid card shows the same dialog and removes the card in place; the
  gallery and details stack at 991 px and sit side by side at 992 px; axe at 375.
- RED: `cd e2e && npx playwright test specs/location-detail.spec.js` → 11 failed
  (`NG04002: Cannot match any routes. URL Segment: 'locations/…'`; no card Delete).
  Two implementation fixes after wiring: axe contrast on `--lp-color-text-3` text
  (the app uses `--lp-color-text-2` for readable secondary text, as its hints do),
  and focus returning to More actions after a save (the dialog now closes its
  `<dialog>` before emitting `saved`, as the delete dialogs do).
- Built: `api` `ILocationService.get/update/updateText/setTags` +
  `LocationTextField`, `IDeletionService.deleteLocation` (HTTP `DELETE
  /api/locations/{id}?revision`), mocks bridged to `loupeLocations`; `domain`
  `LocationDetailPanel` (load/retry/unavailable, head with menu, address lines,
  coordinates, dates, report placeholder), `LocationGallery` (viewing: contain
  stage, radio thumbnails, cover pill, caption, capacity, Add images output),
  `LocationTextEditor` (brief/notes, explicit Save, status, conflict review),
  `LocationTags` (chips + explicit Save, status, reload on conflict),
  `LocationForm.mode="details"`; `loupe` `LocationDetailPage` (route
  `locations/:id`, `locationUnsavedGuard`, notice), `EditLocation` (revision
  conflict → Reload latest keeping attempted values, field errors, retry),
  `DeleteLocation` (loads latest for revision and image count, names effects),
  grid card Delete action through `LocationCollection.deleteRequested/remove` and
  the `locationDeleted` router state notice; `--lp-stage-max-height` token in
  `design-system/src/tokens.css`.
- GREEN: `specs/location-detail.spec.js` → 11 passed; band `location-detail`,
  `locations`, `locations-add`, `search-navigation`, `reference-delete`,
  `photograph-deletion` → 58 passed; `npm run build` clean apart from the
  pre-existing budget warning; prettier clean; `design-system` `npm test` → 70
  passed after the token addition.
- Non-claims: L2-055.5's report-snapshot label and Regenerate, and the report
  status pills for Queued/Running/Ready/Outdated/Failed, are exercised in B4 (the
  pill mapping exists but the fixture only produces `None`/`Ready` today);
  upload, remove and cover actions in the gallery are A8; L2-057.5's keyboard
  arrow selection is asserted in A8 with the radio group already in place.

### A8 — Manage location images in the gallery (L2-056.1–.6, .9 UI; L2-057.5)

- Spec: `e2e/specs/location-images.spec.js` (7 cases) with the fixture's
  `addImage` (per-key receipts, cap, `unsupported_media`, per-key gates,
  `abortUpload` tracking, `reportProgress` on `loupe-location-upload-progress`),
  `removeImage` (renumbering, cover hand-off) and `setCover`: two chosen files
  become two `addImage` calls with distinct keys, each row shows `Uploading…`,
  a progress event for one key drives only that row's `<progress>` (400/1000),
  both rows show `Saved`, the gallery grows to five with `5 of 10 images` while the
  dialog is still open, Done closes and image 5 is selectable on the stage; with
  three files, the first saves, the second fails with `didn't upload` + Retry/
  Remove, the `image/tiff` file is refused client-side with `a format Loupe can't
  read` + Remove and never reaches the bridge, Retry saves it and Remove clears
  the row; eight existing images + three chosen → `You chose 3 images, but only 2
  more fit.` before any call, two then save and Add images is disabled with `10 of
  10 images. Remove one to add another.`; Remove image 2 → dialog `Remove image
  2?`, Cancel keeps three, confirm → two with image 1 still the cover and `2 of 10
  images`, removing the cover moves it to the next; Set as cover is disabled on
  the cover, choosing image 3 persists across reload and the grid card shows a
  cover; Cancel remaining while a transfer is held aborts it (bridge `abortUpload`),
  leaves three images, shows `Uploads stopped… Refresh to check.` and Refresh
  reveals what the server finished; arrow keys move the radio selection with the
  stage and announced caption following, axe clean.
- RED: `cd e2e && npx playwright test specs/location-images.spec.js` → 6 failed
  (no `Add images` dialog, no Set as cover/Remove controls); the keyboard case
  already passed on A7's radio group and confirms rather than drives it. Two
  spec fixes after wiring: the dialog's accessible name changes to `Uploading N
  images…`/`N of M images added` as the mock does, and the grid check needed the
  sign-in step after navigation.
- Built: `api` `ILocationService.addImage(id, file, operationKey, onProgress?,
  signal?)`, `removeImage`, `setCover` (multipart POST with progress events and
  abort, DELETE with revision, PUT cover); mock bridge with progress filtering by
  key and `abortUpload`; `domain` `LocationImageUpload` (capacity check before any
  transfer, per-file queue with own key, progress, `AbortController`, client-side
  format/size rejection, Retry/Remove per row, Cancel remaining), `LocationGallery`
  Set as cover (service call, conflict message) and Remove request, focus helper;
  `loupe` `AddLocationImages` (title by phase, Cancel/Upload → Cancel remaining/
  Done), `RemoveLocationImage` (revision-checked, reload on conflict),
  `LocationDetailPage` wiring with the `Uploads stopped` notice and Refresh, and
  uploads cancelled when leaving the page after the unsaved prompt.
- GREEN: `specs/location-images.spec.js` → 7 passed; `npm run build` clean apart
  from the pre-existing budget warning; prettier clean. Full `cd frontend && npm
  test` (end of group A) → 675 passed, 1 failed: `search-layout.spec.js` at
  640×800 with increased text spacing flagged the fifth navigation link as
  obscured by Sign out (axe `target-size`) — a real regression from the fifth
  area, not a flake. Fixed in `app.css`: between 640 and 767 px the Library
  navigation shows icons with visually hidden labels (accessible names unchanged),
  and `search-layout`, `search-navigation`, `locations` and `sign-in-layout` pass
  again (34 cases).
- Non-claims: the report-outdating half of L2-056.5 lands with B3/B4 (the remove
  dialog states it); drag-and-drop is not offered (the file picker is the non-drag
  path); HEIC previews come from the server preview URL, not the browser.

### B1 — Admit a scouting report request (L2-060.1, .2, .7; L2-033.1; L2-035)

- Tests: `backend/tests/Loupe.Api.Tests/Scouting/ScoutingAdmissionTests.cs`
  (4 cases) with `ApiFactory` in Live mode and a `ControlledAiTransport` that
  counts provider calls: `POST /api/locations/{id}/scouting-report` on an imaged
  location → 202 with `Location: /api/operations/{id}`, type `LocationScouting`,
  `Queued`, `Live`; the same `Idempotency-Key` replays the identical body; a
  repeat without the key and a `regenerate` request while the job is active both
  return the active job; the detail and grid card report `Queued` with `report`
  null, `…/scouting-report/operation` returns the job and `…/scouting-report` →
  204; notes remain editable; zero provider calls; after an API restart the job
  is still `Queued`. No images → 400 naming `images` with nothing queued and
  `None` unchanged. Without `Ai:ApiKey` → 503 `integration_not_configured` with
  `Retry-After`, nothing queued, and the brief still saves. Five admitted jobs →
  the sixth 429 with `Retry-After` and no operation; a stranger → 404; a stale
  revision → 409; revision 0 → 400.
- RED: `-Filter 'FullyQualifiedName~ScoutingAdmissionTests'` → `Failed: 4,
  Passed: 0` — the route returned 404 (`Expected: Accepted / Actual: NotFound`).
- Built: `Loupe.Domain/Scouting/{ScoutingInput,ScoutingImageInput}`,
  `OperationType.LocationScouting`, `Location.CurrentScoutingOperationId`
  (navigation to `BackgroundOperation`, `SetNull`) and `ScoutingReportJson`
  (migration `LocationScouting`); `IScoutingConfiguration`/`ScoutingConfiguration`
  (`location-scouting-v1`), `IScoutingStore`/`ScoutingStore.AdmitAsync`
  (admission lock, row lock, revision, `images` field error, active job returned,
  five-active `AnalysisLimitException`, input built from the ordered images'
  preview keys and allowlisted EXIF, the image-set revision and the brief);
  `RequestScoutingReportCommand[Handler]` (identity resolved before any work,
  receipt type `scouting`), `GetScoutingOperationQuery[Handler]`,
  `GetScoutingReportQuery[Handler]` (204 until a report shape exists),
  `ScoutingReportsController`, `RequestScoutingReportRequest`;
  `LocationReportStatus.Derive` feeds `reportStatus` on the detail and the grid.
- GREEN: same filter → `Passed: 4`. Band `Locations|Scouting|Search|Admission|
  CritiqueLease|Operations` → `Passed: 136`. `dotnet build` clean; `dotnet format`
  clean for the new files.
- Non-claims: reuse of a Succeeded report without a provider call and explicit
  Regenerate after success (L2-060.5) need a completed job, so they are proven in
  B2 with the worker; the `GET …/scouting-report` body is typed in B2.

### B2 — Generate, validate, and publish a scouting report (L2-058.1–.7; L2-059.3, .4; L2-060.4, .5, .6, .8; L2-034)

- Tests: `backend/tests/Loupe.Api.Tests/Scouting/ScoutingWorkerTests.cs` (23
  cases) hosting the real `AnalysisWorker` against a `ControlledSourceTransport`
  and the `ScoutingReportFixture` builder (a complete valid provider result citing
  image numbers): a three-image location whose address, coordinates, notes and
  tags carry SECRET markers reaches `Succeeded`; the single recorded request
  contains the brief, `"store":false`, a strict `json_schema`, three
  `input_image` parts and no `"tools":` property, and none of the secrets or
  coordinates; `GET …/scouting-report` returns `mode` Live, `location-scouting-v1`,
  the model, the brief snapshot, `imageCount` 3, the image-set revision and a
  timestamp; the report's sections are `overview, suitability, timesOfDay,
  techniques, groupSize, cautions` in that order, with the five shoot types, the
  seven periods, ratings and bases as sent, and every entry's `citedImageIds`
  resolved to the real ids of images 1 and 3; no `score` appears; the detail is
  `Ready` with the same report and untouched notes, the card is `Ready`, a stranger
  → 404. Twenty invalid shapes (missing section, foreign rating/period/technique/
  basis, blank and 1,001-rune text, duplicate or missing shoot type, four
  strengths, thirteen or duplicate techniques, no Recommended while Avoid is
  present, Recommended on an Inferred basis, an image number outside the set, no
  cited image, a numeric `score`, group 0/501/min-above-max) each retry once
  (`Queued` + `invalid_output`, one call), then after 5 s fail `invalid_output`
  with a safe message, two calls, `…/scouting-report` → 204 and the detail
  `Failed` with `report` null. An unchanged request after success returns the same
  operation with no provider call; `regenerate` creates a new job, the previous
  report stays visible while it runs (`Running`), a second regenerate returns the
  active job, and on commit the second report replaces the first with notes
  unchanged (two calls in all). After a success, a regenerate that meets 503 →
  `Queued provider_unavailable`, then 429 → `Queued provider_rate_limited`, then a
  refusal → `Failed unsupported_input` with the earlier report still readable.
- RED: `-Filter 'ScoutingWorkerTests.L2_058_1|ScoutingWorkerTests.L2_060_5'` →
  `Expected: "Succeeded" / Actual: "Queued"` (nothing processed
  `LocationScouting`) and an empty `…/scouting-report` body. One later failure
  was the spec's own: the prompt legitimately says "call tools", so the request
  check now asserts the absence of a `"tools":` property.
- Built: `Loupe.Domain/Scouting` report types (`ScoutingReport`, `ReportStrength`,
  `SuitabilityEntry`, `TimeOfDayEntry`, `TechniqueEntry`, `GroupSizeEntry`,
  `CautionEntry`, `SavedScoutingReport`) and the shared vocabularies as enums with
  `JsonStringEnumMemberName` labels; `ScoutingReportValidator` (every L2-058 rule,
  Recommended/Avoid must be Visible, a supportable set needs a Recommended period,
  cited ids ⊆ analysed set); `IScoutingProvider`, `IScoutingWorkStore`,
  `RunScoutingReportCommand[Handler]` (the reference-analysis handler shape: 120 s
  timeout, 20 s lease renewal, `IAnalysisFailureStore` routing); `ScoutingWorkStore`
  (claim/renew, conditional publish under row locks: current operation and
  image-set revision must still match); `AzureOpenAiScoutingProvider` (numbered
  previews with allowlisted EXIF captions and the brief only, `store=false`,
  `ScoutingPrompt`, `ScoutingResponseFormat` strict schema, `ScoutingOutput`
  mapping image numbers to ids with unknown numbers failing validation);
  `ScoutingStore` reuse of a Succeeded report for unchanged input; `AnalysisWorker`
  round-robin of four kinds and the worker `Program.cs` whitelist;
  `AnalysisFailureStore` scouting wording; `GetScoutingReportQuery` typed as
  `SavedScoutingReport?` and `LocationResult.report`.
- GREEN: `-Filter 'FullyQualifiedName~ScoutingWorkerTests'` → `Passed: 23`. Band
  `Scouting|Locations|ReferenceAnalysisWorker|Critique|PhotographerSummary|
  Operations` → `Passed: 221`. `dotnet build` clean; `dotnet format` clean.
- Non-claims: L2-059.1/.2/.5/.6 are prompt properties and release evaluations —
  recorded as procedure in B5, never as a passed check here; the 60-second lease
  recovery and timeout classes are covered by the shared lease tests, not
  re-proven per job type; UI rendering of the report is B4.

### B3 — Outdate, cancel, and retry scouting work (L2-060.3, .6; L2-056.5; L2-055.5; L2-031.7; L2-034.4)

- Tests: `backend/tests/Loupe.Api.Tests/Scouting/ScoutingLifecycleTests.cs`
  (7 cases): after a report succeeds, adding an image returns `reportStatus`
  `Outdated` with the report still carrying `imageCount` 2 and its original
  `generatedAt`, the grid card shows `Outdated`, removing an image keeps it
  `Outdated` and readable, and Regenerate restores `Ready` with a newer report;
  editing the brief leaves `Ready` with `briefSnapshot` still the first brief;
  while a job is running, deleting the location, adding an image, or removing an
  image marks the job `Canceled` and the released run commits nothing (the
  location is gone, or the report stays absent with status `None`); three 503s
  fail the job with `provider_unavailable` and `Failed` on the detail, a retry
  with a stale revision → 409, `POST /api/operations/{id}/retry` → 202 with a new
  `LocationScouting` job that succeeds (`Ready`), retrying a succeeded or a
  refused (`unsupported_input`) job → 409 `retry_unavailable`; a retryable failure
  whose image set changed since → 409 `analysis_inputs_changed` with no provider
  call.
- RED: `-Filter 'FullyQualifiedName~ScoutingLifecycleTests'` → `Failed: 6,
  Passed: 1` — `Expected: "Outdated" / Actual: "Ready"`, `Expected: "Canceled" /
  Actual: "Running"`, and the retry route answered 404 for a scouting job; the
  brief-edit case already passed on B2 and confirms rather than drives.
- Built: `LocationReportStatus.Derive` compares the saved report's image-set
  revision with the location's (`Outdated`), shared by the detail and the list
  projection; `ScoutingCancellation` cancels the location's active
  `LocationScouting` job under the admission lock from `LocationImageStore`
  add/remove and `DeletionStore.DeleteLocationAsync`; `ScoutingInputBuilder`
  moved to Application for admission and retry; `RetryScoutingReportCommand
  [Handler]` (same retryable codes, `RetryAvailableAt`, revision, identity and
  input comparison → `AnalysisInputsChangedException`, then `AdmitAsync` with
  regenerate); `RetryCritiqueCommandHandler` dispatches `LocationScouting` to it
  before its unchanged critique branch.
- GREEN: same filter → `Passed: 7`. Band `Scouting|Locations|CritiqueManualRetry|
  CritiqueRetry|Deletion|Cleanup|Search` → `Passed: 166`. `dotnet build` clean;
  `dotnet format` clean.
- Non-claims: the Outdated label's count/timestamp display, Regenerate and Retry
  controls are B4; "current for search until a replacement commits" is C1.

### B4 — Request and read the scouting report in the detail (L2-060.1–.8 UI; L2-058.1, .7; L2-057.3; L2-055.5; L2-041.2; L2-045.3)

- Spec: `e2e/specs/scouting-report.spec.js` (7 cases) with the
  `e2e/fixtures/scouting-reports.js` sub-fixture (`window.loupeScoutingReports`:
  operations per location, `advance`/`complete`/`fail`, receipts, `errors.request`)
  attached by the location library, and the detail page object's report helpers:
  an imaged location shows `No scouting report yet`, an enabled Request scouting
  report and the disclosure naming Azure OpenAI, the images, brief and camera
  settings sent and the address, coordinates, notes and tags kept; the request
  acknowledges at once with `Queued for scouting` and the head `Queued` pill, the
  control disappears so it cannot be activated twice, notes stay editable, the
  bridge saw one request with a visible-ASCII key and `regenerate: false`;
  advancing to Running shows `Looking at the images`, completing shows the six
  headings in order and `Report ready` through the 1 s poll; a ready report shows
  `AI generated`, `Generated 10 Sep 2026 · Azure OpenAI · gpt-4.1 · 3 images ·
  brief as saved`, the sections in order, `Not recommended`/`Workable`/`Recommended`/
  `Unknown` pills, `Visible`/`Inferred` bases, `2–8 people`, the caution, no
  score/star/percentage text, and clicking a cited thumbnail selects that gallery
  image; an Outdated report shows the pill and `Based on 2 images as of 10 Sep
  2026…`, Regenerate sends `regenerate: true`, `Regenerating with the current 3
  images` and `Previous report` keep the sections visible until completion shows
  `Generated 12 Sep 2026`; a failed job shows the alert with the safe reason and
  Retry while the previous report stays, Retry calls the retry route; an
  `integration_not_configured` request shows the copy and notes still save; no
  images → the explanation and no request control; a changed brief shows the
  snapshot it used and offers Regenerate.
- RED: `cd e2e && npx playwright test specs/scouting-report.spec.js` → 6 failed
  (no request control, no report rendering); the no-images case already passed on
  A7 and confirms. One spec precision after wiring: a retry with an earlier
  report shows the regenerating state, not the first-request `Queued` copy.
- Built: `api` `scouting-report/` shapes, `IScoutingReportService`
  (`current/get/request/retry`) + `SCOUTING_REPORT_SERVICE`,
  `ScoutingReportService` (204 → null, CSRF + `Idempotency-Key`, error
  parsing), `OperationResult.type` + `'LocationScouting'`,
  `LocationResult.report` typed; `components` `ScoutingReportContent` (six
  sections in order, rating pills, basis, cited thumbnails as `Show image N`
  buttons, no numeric rendering); `domain` `ScoutingReportPanel` (loads the
  current operation, polls every second while active, refreshes the location on
  completion, request/regenerate/retry with `crypto.randomUUID()` keys and a
  single in-flight request, not-configured, failed, outdated, brief-changed and
  no-image states); `LocationDetailPanel` hosts the panel and selects the gallery
  image on citation; `MockScoutingReportService` and both `app.providers.ts`.
- GREEN: `specs/scouting-report.spec.js` → 7 passed; band `scouting-report`,
  `location-detail`, `location-images`, `locations`, `photographer-summary` → 49
  passed; `npm run build` clean apart from the pre-existing budget warning;
  prettier clean.
- Non-claims: B5 records the evaluation manifest; the five-second reflection of
  persisted states (L2-033.2) rests on the 1 s poll as for photographer summaries
  and is not timed separately here.

### B5 — Release evaluation manifest and procedure (L2-059.5, .6; L2-050.2, .5)

- Documentation only, no test: `docs/evaluation/scouting/manifest.json` defines
  the eight fixture slots (open park, urban street with strong lines, window-lit
  interior, narrow alley, waterfront, large hall, playground, night-lit scene),
  each with acceptable observations, prohibited claims and actionability checks,
  the 24-run / 24-contract / 22-evidence gate, and `<TO SUPPLY>` markers for
  assets, licences, briefs, the reviewer and the deployment;
  `docs/evaluation/scouting/README.md` states the procedure, the gate and the
  recording rules; `backend/README.md` adds the scouting step to the live
  connection check.
- Recorded as a procedure: no evaluation has been run and nothing is described
  as passing. L2-059.1, .2, .5 and .6 stay open in `todo.md` until a named
  reviewer runs the evaluation against the production model.

### C1 — Find locations by keyword with shoot filters (L2-062.1, .2, .3, .5, .7; L2-061.2–.5)

- Tests: `backend/tests/Loupe.Api.Tests/ShootPlanning/FindLocationsKeywordTests.cs`
  (6 cases); reports are published through the honest path — admission plus one
  `RunScoutingReportCommand` per location against a queue of canned provider
  results (`ScoutingReportFixture.Valid` mutated per case): every eligible field
  (name, address line, locality, region, postal code, country, setting, brief,
  notes, tag, report caution) matches its own token, `kew slippery` needs both
  the name and the report text, a report-less location matches only its manual
  fields, `citedImageIds` (a JSON key) matches nothing, `café` matches the
  NFC-composed name; shoot types AND over Well suited/Workable (`Engagement` +
  `Events` keeps only the location rated for both, results carry every
  `suitability` rating); people 6 keeps only the 2–8 range and never Cannot
  assess, clearing it restores all four, `groupSize` carries min/max/cannotAssess;
  Golden hour OR Blue hour keeps three of four without duplicates and each
  result lists its `recommendedPeriods`; setting and tags AND (case-insensitive
  tags), a shoot-type, people or time-of-day filter excludes report-less
  locations while setting/tag filters keep them labelled `None`, a blank query
  with filters returns all matching; 11 tags, `Weddings`, `Noon`, `Underwater`,
  people 0/501, a 501-rune query, `pageSize=0` and `mode=semantic` → 400 naming
  the field; a stranger's location, a reference and a photograph never appear,
  `/api/search?query=Kew` returns only the reference, a notes edit is found on the
  very next read, a deleted location leaves `totalCount` 2 and never appears while
  paging with a scoped cursor, and a cursor replayed with another page size or by
  another owner → 400.
- RED: `-Filter 'FullyQualifiedName~FindLocationsKeywordTests'` → `Failed: 6,
  Passed: 0` — `GET /api/locations/search` returned 404. Two SQL faults surfaced
  on the way to GREEN and were fixed: jsonpath does not accept a comma-separated
  path list (one recursive string-value path is used instead) and Npgsql cannot
  type a null scalar in `IS NULL` without a cast.
- Built: `ScoutingReportJson` (stored report JSON now carries the shared enum
  labels, used by the work store, admission reuse, the read query and the
  results); `Loupe.Application/ShootPlanning/{FindLocationsQuery[Handler],
  LocationSearchFilter, LocationSearchCursor, LocationSearchItem,
  LocationSearchGroupSize, LocationSearchSuitability, LocationSearchPage,
  ILocationSearchStore}`, `ScoutingVocabulary` (labels of the shared enums);
  `LocationSearchStore` (parameterized SQL over the owner's locations: NFC and
  case-insensitive substring per token over every eligible field plus every string
  value of the current report, shoot-type/people/time-of-day filters over the
  report JSON, setting and tag filters, report presence only when a report
  filter is set, keyset paging), `LocationSearchRow`; `LocationSearchController`
  `GET /api/locations/search` with `FindLocationsRequest`.
- GREEN: same filter → `Passed: 6`. Band `ShootPlanning|Scouting|Locations|Search`
  → `Passed: 139`. `dotnet build` clean; `dotnet format` clean.
- Non-claims: Meaning mode (`mode=meaning`) is rejected with the shared 400 until
  C4 delivers it; L2-062.8 (URL state) and L2-061.6/.7 (UI) land in C2/C5.

### C2 — Find a location page with keyword results (L2-061.1 card fields, .5, .6 keyword half; L2-062.8; L2-043.2; L2-044.1; L2-045)

- Tests: `e2e/specs/find-location.spec.js` (16 cases) with page object
  `e2e/page-objects/find-location-page.js` and fixture
  `e2e/fixtures/location-search-library.js` (ten named locations over the
  `LocationLibrary` records, reports shaped per location for the shoot filters,
  keyword and filter semantics mirrored in the mock, `loupeLocationSearch`
  bridge); plus one backend seam in `FindLocationsKeywordTests`
  (`L2_061_Tag_filter_choices…`): the filters dialog needs the owner's location
  tags, so `GET /api/locations/search/tags` lists them grouped by identity with
  counts, never a reference's tags or a stranger's. UI cases: the page opens from
  the Locations header link and explains modes and filters without an alert or a
  request (L2-043.2), an example chip runs the search and lands in the URL;
  keyword results show cover or `No images yet` placeholder, name,
  `Locality · N images`, `Recommended · Morning, Golden hour`, `2–8 people` /
  `Group size · Cannot assess`, one `Shoot type · Rating` pill per selected shoot
  type, `No scouting report` on report-less locations, and the card opens the
  detail (L2-061.1, .5); filters combine by AND with a blank query, a people count
  drops Cannot assess and out-of-range locations, Dawn OR Morning, setting, and
  `Clear all` restores the whole library (L2-061.3, .4, .6 keyword half);
  `?q=…&shootTypes=…&people=…&timesOfDay=…&setting=…&tags=…` restores every
  control and re-runs the same request on reload, Back and Forward, No results
  offers Edit query and Clear filters and is distinct from the retryable
  `Find a location isn't available right now.` alert, mode `meaning` round-trips
  through the URL (L2-062.8); the filters dialog lists tags with counts, traps
  focus, Escape and Apply restore focus to their trigger, an eleventh tag is
  refused, a tag load failure offers Retry (L2-045.2, .3); eleven viewport widths
  assert 1/1/1/2/2/3/3/3/3/4/4 columns, the dialog-only filters below 768 px with
  a `5 filters` summary, reachable ≥ 24 px controls, and axe A/AA (L2-044.1,
  L2-045.5, .6).
- RED: backend `-Filter 'FullyQualifiedName~L2_061_Tag_filter_choices'` →
  `Failed: 1` (404); `npx playwright test specs/find-location.spec.js` →
  `16 failed` — no `Find a location` link, no `Describe the shoot` searchbox, no
  `Matching locations` region.
- Built: `ListLocationTagsQuery[Handler]`, `ILocationSearchStore.ListTagsAsync`
  (`location_tags` grouped by `NormalizedName`), `LocationSearchController.Tags`;
  `api/src/lib/location-search/{location-search-request (SHOOT_TYPES,
  TIMES_OF_DAY, LocationSearchFilters, LocationSearchRequest),
  location-search-result, location-search.service.contract
  (ILocationSearchService.search/tags, LOCATION_SEARCH_SERVICE),
  location-search.service}` + `MockLocationSearchService` and both provider sets;
  `components` `LocationResultCard` and `LocationSearchFilters` (chip rows, people,
  setting segmented control, Tags/Filters trigger with count, Clear all; `domain`
  `LocationSearchResults` (results head, `--lp-collection-columns` grid, skeleton,
  No results, invalid-request, cursor-refresh and retryable error states, Load
  more with focus continuity); `loupe` `FindLocationPage` (URL-held query, mode
  and filters, validation of URL values, tag loading) and
  `LocationSearchFiltersDialog`; route `locations/find` ahead of
  `locations/:id`; `Find a location` header link in `LocationCollection`; token
  `--lp-collection-columns` (1/2/3/3/4) in `design-system/src/tokens.css`; shared
  `.lp-chips`, `.lp-segmented*`, `.lp-input--count`, `.lp-small` in `styles.css`;
  `App` keeps focus in place for query-only navigation on `/locations/find` as it
  already did for `/search`.
- GREEN: spec → `16 passed`; band `locations`, `locations-add`,
  `location-detail`, `search`, `search-navigation`, `search-layout`,
  `search-filters`, `sign-in` → `107 passed`; backend band
  `ShootPlanning|Search` → `Passed: 55`; `dotnet build` clean; `dotnet format`
  clean for touched files; `npm run build` clean; prettier clean.
- Review notes: `LocationSearchFilters` sits in `components` rather than the
  design's `domain` because it injects no service — the placement rule in
  `AGENTS.md` decides. The results card shows ratings only for the selected shoot
  types (the design's rule); a location with a report and no selected type shows
  no pill.
- Non-claims: Meaning mode is selectable and round-trips through the URL, but
  its results label, blank-query prompt, unavailable and refresh-required states
  are C5 (the production API still answers `mode=meaning` with the shared 400
  until C4); L2-061.1 and L2-061.6 stay open for their Meaning halves;
  L2-043/044/045 are cross-area criteria and stay unticked on this branch.

### C3a — Record location index intents and report index status (L2-028.2; L2-062.4, .5 — status half)

- Tests: `backend/tests/Loupe.Api.Tests/Search/LocationIndexTests.cs` (3 cases):
  create, details, notes, tags, image add and image remove each acknowledge with
  `indexStatus: "updating"` and one queued `LocationIndex` operation
  (`indexOperationId`, readable at `/api/operations/{id}` with `resourceId` =
  the location) that coalesces while queued, and deleting the location cancels
  it; a requested scouting report reports `processing-report` while queued or
  running and a refused report (`unsupported_input`, run through the hosted
  `AnalysisWorker`) leaves the location `updating` on its prior intent; without
  `Embeddings:Endpoint` the status is `not-configured` while the record and
  keyword search behave the same.
- RED: `-Filter 'FullyQualifiedName~LocationIndexTests'` → `Failed: 3` —
  `indexStatus` absent from the location body (`KeyNotFoundException`).
- Built: `OperationType.LocationIndex`; `Location.CurrentIndexOperationId` +
  navigation (migration `LocationIndex`, FK set-null, index);
  `IEmbeddingConfiguration` (Application/Search) with `EmbeddingOptions`
  (`Embeddings:Endpoint`, `Embeddings:Model` default `bge-m3`, validated on
  start) and `EmbeddingConfiguration`; `LocationIndexIntent.RecordAsync`
  (queued intents coalesce, a running one is superseded and canceled, the new
  operation carries the model identity and `location-document-v1`) called from
  every revision bump — `LocationStore.SaveAsync/EditAsync`,
  `LocationImageStore` add/remove/cover, `ScoutingStore` report reuse,
  `ScoutingWorkStore.PublishAsync` — and `LocationIndexIntent.CancelAsync` from
  `DeletionStore.DeleteLocationAsync`; `LocationIndexStatus.Derive`
  (not-configured → processing-report → current/failed/updating);
  `LocationResult.IndexStatus/IndexOperationId` through every location handler;
  the nine requested-job caps (`>= 5` active) now exclude `LocationIndex`, the
  maintenance type the design exempts.
- GREEN: same filter → `Passed: 3`. Band
  `Locations|Scouting|ShootPlanning|Search|Critiques|References|Photographer|Deletion`
  → `Passed: 512`. `dotnet build` clean; `dotnet format` reports no finding on a
  changed line (the pre-existing whitespace findings in `PhotographerDraftStore`,
  `ReferenceAnalysisStore/Queue`, `PhotographerSummaryStore/Queue` sit on
  untouched lines).
- Non-claims: the `search_vectors` table and `CREATE EXTENSION vector` the plan
  listed here move to C3b, where the first test writes a vector; `current` needs
  the worker (C3b) and stale-vector exclusion the query-time join (C4), so
  L2-062.4/.5 stay open; the frontend `LocationResult` shape gains
  `indexStatus` in C5 where it is rendered.

### C3b — Embed location documents through Ollama and keep vectors current (L2-028.1, .4, .5 for locations; L2-062.4, .6, .7 API; L2-041)

- Tests: `backend/tests/Loupe.Api.Tests/Search/LocationIndexWorkerTests.cs`
  (6 cases) hosting `SearchIndexWorker` against a `ControlledEmbeddingTransport`
  on the `ollama` client (`ApiFactory.EmbeddingTransport`): one worker pass
  moves a saved location to `current`, its operation to `Succeeded`, and the
  `search_vectors` row to the location's `Revision`, while the single
  `POST http://ollama.test:11434/api/embed` body names `bge-m3` and carries the
  name, locality, region, country, setting, tags, brief and notes but never the
  address lines or postal code; a report requested with the save reports
  `processing-report`, then the published report re-embeds a document holding
  the caution text and period labels; an edit acknowledged while the first
  embedding is in flight (gated transport) cancels that run, records a new intent,
  and the replacement is built from the edited notes with no vector left from
  the first run; a 503 from the endpoint retries twice under the shared clock
  and then reports `failed` while keyword search still finds the location and
  no vector exists, a stale revision on `POST /api/operations/{id}/retry` → 409,
  the right revision → 202 with a new `LocationIndex` operation that the worker
  completes to `current`; deletion removes the vector row; without an endpoint
  the worker sends nothing and the intent stays queued. Requests are matched by a
  per-test marker because the acceptance database (and its queued intents) is
  shared across the class.
- RED (two steps, the `IDnsResolver` precedent): HTTP-only the tests cannot host a
  worker that does not exist, so the seam landed first — `SearchIndexWorker`,
  `RefreshLocationSearchDocumentCommand` with a handler returning false,
  `IEmbeddingProvider`, `ApiFactory.EmbeddingTransport` — then
  `-Filter 'FullyQualifiedName~LocationIndexWorkerTests'` → `Failed: 5,
  Passed: 1`: status stayed `updating`, the operation stayed `Queued`, no
  request reached the transport, and `relation "search_vectors" does not exist`
  (the idle-when-unconfigured case is true of the seam and stays green).
- Built: migration `SearchVectors` (`CREATE EXTENSION IF NOT EXISTS vector`,
  `search_vectors(OwnerId, ItemType, ItemId, ModelIdentity, SourceRevision,
  Vector vector(1024), IndexedAt)` with an HNSW cosine index, raw SQL outside
  the EF model); `LocationSearchInputBuilder` (document without address lines,
  report flattened through `ScoutingVocabulary.Label`); `LocationIndexSource`,
  `ILocationIndexWorkStore`/`LocationIndexWorkStore` (claim through the shared
  lease store, read, conditional publish — the location must still point at the
  operation at the same revision, else the run is canceled as superseded or
  deleted — `real[]::vector` upsert, requeue); `RefreshLocationSearchDocumentCommandHandler`
  (120 s timeout, lease renewal, provider/timeout rejection through
  `IAnalysisFailureStore`); `OllamaEmbeddingProvider` (`POST {Endpoint}/api/embed`,
  named client `ollama`, 429 → rate limited, 404 → disabled, 400 → unsupported,
  other failures transient, wrong dimension → invalid output);
  `RetryLocationIndexCommand[Handler]` behind the shared retry route (Failed
  `LocationIndex` only, revision check, no input comparison, receipt
  `retry-index`); `DeletionStore` deletes the vector row; worker `Program.cs`
  hosts `SearchIndexWorker` and whitelists the handler; `backend/README.md`
  documents `Embeddings:Endpoint/Model`, the pgvector requirement and the
  connection check.
- GREEN: same filter with `LocationIndexTests` → `Passed: 9`. Band
  `Locations|Scouting|ShootPlanning|Search|Critiques|Deletion|Operations` →
  `Passed: 283`. `dotnet build` clean; `dotnet format` clean on touched files.
- Review notes: the plan's generic `IVectorWriter`/`PgVectorStore` names became
  the single-use `ILocationIndexWorkStore`/`LocationIndexWorkStore` — one port
  for one worker; C4 adds the ranking read to `ILocationSearchStore`. Index
  operations reuse the shared lease store and its per-owner/global concurrency,
  and the requested-job caps exclude them (C3a).
- Non-claims: the 60 s freshness window is asserted as "current after one worker
  pass" under the shared clock; the wall-clock budget is not measured. Meaning
  search reading these vectors is C4, so L2-062.4/.6 stay open until the vectors
  are searchable and the detail shows the states (C5). No real Ollama call was
  made; the live smoke is the end-of-branch step.

### C4 — Rank locations by meaning (L2-061.1 API, .6 API, .7 API, .8; L2-062.4, .5, .7 API; L2-026.3, .4, .6 for locations)

- Tests: `backend/tests/Loupe.Api.Tests/ShootPlanning/FindLocationsMeaningTests.cs`
  (6 cases) with a `ControlledEmbeddingTransport` that embeds each seeded
  location's document to a hand-built unit or blended vector and the query to
  axis one, indexing through a hosted `SearchIndexWorker`: the canonical query
  returns the relevant location first (score 1.0, then 0.6) without any query
  word in its tags, drops the orthogonal one, carries the card fields and embeds
  the query exactly once; a setting filter narrows the ranked set before paging
  (two pages of one, no underfill, no third page) and a 0.1 score never
  appears; equal scores order by identifier across pages, the cursor is refused
  by another page size (400) and by a second `ApiFactory` under
  `Embeddings:Model=other-model` with 409 `refresh_required`, whose own Meaning
  search compares nothing built under the old model; a blank or whitespace
  Meaning query → 400 `query` with no embedding request while Keyword browses
  by filters; a 500 from the endpoint and an unconfigured endpoint → 503
  `search_unavailable` while Keyword still finds the location; a notes edit
  removes the location from Meaning results and counts until its re-index
  lands while Keyword finds the edit at once, and a deleted location leaves
  results and counts.
- RED: `-Filter 'FullyQualifiedName~FindLocationsMeaningTests'` → `Failed: 6`
  — `mode=meaning` answered the shared 400. One fault surfaced on the way to
  GREEN and was fixed: the keyword token predicate was still applied in Meaning
  mode (every query word had to occur in the text), so Meaning matched nothing.
- Built: `SemanticCursor` (Application/Search: score+id keyset bound to the
  search scope and a model-identity generation segment → 400 for another
  search, 409 `refresh_required` for another model);
  `SearchUnavailableException` (503 `search_unavailable`, Retry-After 5) and
  `SearchRefreshRequiredException` (409 `refresh_required`);
  `LocationSearchRanking` (model + query vector, 0.20 threshold);
  `ILocationSearchStore.RankAsync/CountRankedAsync` and the store's shared
  `Filtered` SQL now carries a `Score` column — `1 - (Vector <=> query)` from
  the `search_vectors` row of the configured model at the location's current
  `Revision`, null otherwise — so ranking, threshold, filters and keyset paging
  compose over one query; `FindLocationsQueryHandler` meaning branch (blank
  query refused before embedding, unconfigured or failing provider →
  unavailable, tokens only in Keyword); `LocationSearchItem.Score` is
  `[JsonIgnore]`d — no score reaches the client.
- GREEN: same filter → `Passed: 6`. Band
  `ShootPlanning|Search|Locations|Scouting` → `Passed: 155`. `dotnet build`
  clean; `dotnet format` clean on touched files.
- Non-claims: relevance of a real model is C6's reviewer-run evaluation; the
  hand-built vectors prove ranking, threshold, ties and generation handling
  only. L2-061.1/.6/.7/.8 and L2-062.4/.5/.6 keep their UI halves for C5;
  L2-062.7 is now proven for both modes.

### C5 — Meaning mode in the Find a location page (L2-061.1, .6, .7, .8 UI; L2-062.4, .5, .6 UI)

- Tests: `e2e/specs/find-location.spec.js` (+3 cases) and
  `e2e/specs/location-detail.spec.js` (+1): Meaning results carry the
  `Matched by meaning` pill and keyword results never do, `search_unavailable`
  shows "Meaning search isn't available right now." with Switch to Keyword
  (re-runs the same query and filters in Keyword mode, URL without `mode`) and
  Try again; a blank Meaning query (`?mode=meaning&setting=Outdoor`, or choosing
  Meaning with an empty box) marks the field invalid, shows "Describe the shoot to
  search by meaning." with Switch to Keyword and makes no search call, and
  Keyword then browses the seven Outdoor locations; `refresh_required` on Load
  more keeps the 24 loaded cards under the "Your locations changed while you were
  browsing." notice whose Refresh results reloads from the top without a cursor;
  the detail shows `Processing report`, follows the fixture to `Updating search`
  by polling, shows `Search indexing failed · Retry` while notes still save,
  Retry posts the failed operation id with the revision and an idempotency key
  and returns to `Updating search`, and `current` or `not-configured` show no
  pill. Fixture growth: `LocationLibrary.indexing(item, status, operationId)`,
  `retryIndex` bridge with revision/retry-unavailable checks, and the search
  fixture refusing blank Meaning queries as the API does.
- RED: `-g "L2-061.1 / L2-061.7|L2-061.6:|L2-061.8:|L2-062.4 / L2-062.5"` →
  `4 failed` (no `Matched by meaning`, no query-required state — two alerts
  resolved, no `Search index` status).
- Built: `LocationResult.indexStatus/indexOperationId` (`LocationIndexStatus`
  type), `ILocationService.retryIndex` → `POST /api/operations/{id}/retry` with
  the mock bridge; `domain` `SearchIndexStatus` (status pill named
  `Search index`, 2 s polling of the location while processing-report/updating,
  Retry with a stable idempotency key and safe error copy) hosted in
  `LocationDetailPanel`'s meta row; `LocationSearchResults` gains the meaning
  pill, the unavailable state with `keywordRequested`, and the
  refresh-required notice (`refresh_required` or an invalid cursor) that keeps
  loaded cards; `FindLocationPage` refuses a blank Meaning query before any
  request (`queryRequired` field error + compact state) and `useKeyword()`;
  `.lp-notice__body/title/actions` shared classes.
- GREEN: same selection → `5 passed`; band `find-location`,
  `location-detail`, `scouting-report`, `location-images`, `locations`,
  `locations-add`, `search`, `search-navigation` → `84 passed`;
  `npm run build` clean; prettier clean.
- Non-claims: the index status is not shown on the Locations grid cards (the
  spec places it on the detail); relevance of live Meaning results is C6.

### C6 — Relevance evaluation corpus and procedure (L2-061.9; L2-047 seed note)

- Written: `docs/evaluation/shoot-planning/manifest.json` (30 corpus location
  slots with name, setting, tags and brief fixed and images, notes, licence
  `<TO SUPPLY>`; 8 queries — the three example searches on the Find a location
  page among them — each with at least three pre-labeled relevant locations;
  the gate of ≥ 3 relevant in the first five for ≥ 6 queries and ≥ 2 for every
  query; reviewer and model digest `<TO SUPPLY>`), `manifest.schema.json`, and
  `README.md` with the setup (reports generated by the production scouting
  model, vectors current before any query), the procedure (five-result Meaning
  queries saved per release, reviewer reasons, ownership and filter checks), the
  gate and its invalidation on model, threshold or document-version change, and
  the `L2-047` load-profile seed note (200 locations × 4 images per user, 80 %
  with report and current vector, categories drawn from the Locations
  endpoints, semantic queries on their own budget, seed through the public API).
- Non-claims: no evaluation and no capacity run has been executed; both are
  recorded as not run, never as passing (`L2-050.5`). L2-061.9 and L2-047 stay
  unticked. The same "procedure, not a pass" stance as B5.

### D — Design-system examples and wrap-up (L2-051.3, L2-052.1 for Locations)

- Tests: `design-system/tests/specs/locations.spec.js` (9 cases) with page
  object `tests/page-objects/locations-page.js`: the reference navigation
  reaches `/locations.html`; six location cards show Report ready, Outdated,
  Queued, Scouting, Failed and the no-image placeholder with No scouting
  report; result cards show place, Recommended periods, group range or Cannot
  assess and the suitability pills, or the No scouting report pill; the gallery
  radio group switches the stage by click and by ArrowRight/ArrowLeft with the
  selection announced, and Set as cover renames the cover; the report lists the
  six sections in order with rating and basis pills and no numeric score, and a
  cite button selects and focuses the thumbnail; the status pills read
  Processing report, Updating search and Search indexing failed · Retry, and
  Retry moves to Updating search; the Meaning mode shows Matched by meaning and a
  shoot-type chip toggles; axe A/AA clean and no request leaves 127.0.0.1;
  eight widths assert the 1/2/3/3/4 result columns without horizontal overflow.
- RED: `npx playwright test tests/specs/locations.spec.js` → `9 failed` (no
  Locations link, no results list).
- Built: `design-system/locations.html`, `src/locations.css` (tokens only),
  `src/locations.js` (gallery, cover, cite, mode pill, chips, retry), the Vite
  input and reference navigation link, `design-system/README.md` section;
  `README.md` Features row for Locations; `docs/detailed-designs/README.md`
  overview now lists Locations, scouting and shoot planning as implemented with
  Meaning search for locations, leaving the inspiration library's Meaning search
  proposed.
- GREEN: full design-system suite → `79 passed`.
- Non-claims: L2-052.1 visual review and L2-052.2 baselines are review
  obligations, not automated here; the design-system item L2-051.3 covers every
  area and stays a shared tick.

### Final verification (end of branch)

- Backend: `./backend/Test.ps1` (full) → `Passed: 685, Failed: 0` (8 m 32 s);
  the eleven `RobotsPolicyTests` failures recorded on `main` did not reproduce in
  this run. `dotnet build` clean; `dotnet format` clean on every file this branch
  touched (pre-existing whitespace findings in untouched files remain a
  non-claim).
- Frontend: `cd frontend && npm test` (full Playwright, Chromium) → `703 passed`
  (17.7 m); `npm run build` clean (the 500 kB initial-bundle budget warning is
  pre-existing).
- Design system: `npm test` in `design-system/` → `79 passed`.
- Real-stack smoke through `docs/demo/harness/setup.ps1 -Prefix loupe-loc-smoke
  -Ollama` (real Api and Worker in Linux containers, PostgreSQL/pgvector, a local
  `ollama/ollama` container with `bge-m3` pulled): sign-in 200; create a location
  with locality, region, country, setting, brief, notes and two tags → 201 with
  `indexStatus: updating`; two PNG uploads → 201 each (cover set); the scouting
  request → 503 `integration_not_configured` because no Azure OpenAI
  credentials are configured on this machine — **the scouting report against a
  real provider is recorded as not run**; the `LocationIndex` operation reached
  `Succeeded` ("Search is current.") through the real Ollama endpoint and the
  detail reported `current`; `GET /api/locations/search?query=arches&setting=Outdoor&tags=river`
  → 1 result; `mode=meaning&query=golden hour couple session by the river` →
  the location (real `bge-m3` similarity above 0.20); a blank Meaning query →
  400; `GET /api/locations/search/tags` → the two tags with counts. Through the
  served Angular app (real HTTP adapters, same-origin proxy): sign in, Find a
  location in Meaning mode → "1 location for …" with the `Matched by meaning`
  pill, the result opens the detail, which shows the request-a-report call to
  action and no index-status pill (current). Torn down with `teardown.ps1`.
- Non-claims: the Azure-backed scouting report and the reviewer-run evaluations
  (`docs/evaluation/scouting`, `docs/evaluation/shoot-planning`) were not run;
  the `L2-047` capacity run was not run; `L2-052` visual review is a review
  obligation. The harness's `ng serve` had to be restarted by hand once because
  the `domain` library build was cut short when the setup script was moved to
  the background — a session artefact, not a harness defect.

### Narrated demo recording (`docs/demo/locations/`)

- `locations-tour.mp4` — 199.3 s, 1440 × 1040, 13.96 MB, ten chapters, narration
  mean −16.2 dB / peak −1.5 dB, 52 caption cues (burned in and as SRT/VTT),
  chaptered player `index.html`, `transcript.md`, `chapters.json`, `poster.jpg`,
  `verification.md`. One continuous Chromium take of the real application against
  the real Api, Worker, PostgreSQL/pgvector and a local Ollama `bge-m3`
  (`setup.ps1 -Ollama`): sign-in, browse, add a location, three real uploads and a
  cover change, inline notes/tags with the live `Updating search` → current cycle,
  the scouting request refused with `integration_not_configured` (no Azure
  credentials — the one expected 503), a synthetic report on the design-system
  site badged as such, keyword search with setting and tag filters, and Meaning
  search ranked by the real model (Kew › Hampstead › Walthamstow), then the
  persisted state re-read through the API. Pipeline in `e2e/demo/locations/`
  (`seed.mjs`, `record.mjs --dry`, `narrate.py`, `record.mjs`, `assemble.py`,
  `verify-player.mjs`, `verify-media.py`), mirroring the Inspiration tour.
- Harness: `reset-content.ps1` now empties the Locations tables and
  `search_vectors`; `setup.ps1 -Ollama` waits for the Ollama server, shares one
  `loupe-ollama-models` volume across stacks and keeps the model resident
  (`OLLAMA_KEEP_ALIVE=24h`).
- Non-claims: no scouting report was generated (no provider); the design-system
  chapter is synthetic and labelled; Meaning relevance beyond this eight-location
  library is the reviewer-run evaluation, not this recording.

