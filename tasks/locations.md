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

