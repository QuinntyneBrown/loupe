# Acceptance evidence

Record the command, observed red failure, green result, and review for each slice.
No application behavior was implemented before this log was created.

## API-01: reject anonymous session reads

- Repository-local NuGet source configuration avoids a missing machine-level
  offline feed; this setup failure was resolved before recording behavioral red.
- Red: `dotnet test backend/Loupe.slnx --filter FullyQualifiedName~AnonymousSessionTests`
  compiled successfully and failed with expected Unauthorized, actual NotFound.
- Green: the same command passed; all current backend tests compiled with warnings
  as errors. `dotnet format backend/Loupe.slnx --verify-no-changes --no-restore` passed.
- Reviewed thin MediatR controller, inward dependencies, one type per file,
  non-cacheable safe errors and absence of client-controlled ownership.
  This is partial L2-037/L2-038 coverage, not completed identity integration.

Reference sources: [Playwright web servers](https://playwright.dev/docs/test-webserver),
[Angular compatibility](https://angular.dev/reference/versions),
[ASP.NET OIDC](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0).

## DS-01: independent keyboard-operable button

- Red: `cd design-system; npm test -- --project=chromium` served the built blank
  page successfully, then failed because the reference heading was absent.
- Includes part of L2-051.1/.3 and L2-045.1; these whole criteria remain open until
  all their examples and workflows are delivered.
- Green: `npm test` built the static site and passed all 3 browser tests. Desktop (1440) and mobile (375) screenshots inspected; no clipped content. Review: native keyboard button behavior, inert synthetic content, token-based authored styles, no runtime service calls. No full accessibility conformance claim.

## DS-02: navigable token reference

- Red: focused Chromium token test failed on missing Tokens navigation.
- Green: npm test passed 48 checks across Chromium/Firefox/WebKit, including 14 viewport sizes per engine, axe A/AA scans, no external requests, and no runtime exceptions. Reviewed inert DOM construction and direct use of authoritative values. Typography/motion specimen refinements remain part of the gallery slice.

## DS-03: editor dialog

- Red: focused Chromium test failed because the editor trigger was absent.
- First implementation exposed focus escape in Chromium/WebKit; retained assertions and added explicit Tab wrapping.
- Green: npm test passed 60 browser checks, including modal keyboard containment, error accessibility scans, save/cancel focus recovery and the full token viewport regression. Build passed within the test command.

## DS-04: component states and image-grid specimens

- Red: gallery acceptance failed on missing States & layouts navigation.
- Green: 13 focused Chromium gallery checks passed. Full two-worker regression: 98 passed, one WebKit dialog exceeded aggregate 30s after successful scan/save/cancel actions. Four affected WebKit dialog checks then passed with a 60s total harness allowance; every 5s interaction assertion and all L2 product budgets remain unchanged. No assertions or accessibility rules removed.
- Reduced runner parallelism from four to two after traces identified scanner contention. Port 4173 was occupied by an unrelated Python server; moved this project's preview/tests to 4187.
- Reviewed desktop rendering, generated breakpoint behavior, inert synthetic examples, and JS syntax. Full named manual accessibility/visual approval remains a release gate.

## API-02: OIDC challenge and local return destinations

- Red: challenge test received 404 instead of redirect. The controlled metadata fixture was corrected to replace ConfigurationManager, preventing any external discovery request.
- Green: code/PKCE/state/nonce challenge and anonymous regression passed.
- Red: all five malicious-return tests received redirects instead of field errors.
- Green: all seven backend API tests and dotnet format --verify-no-changes passed after Application validation and safe exception mapping.
- No OIDC completion, durable-session or real Keycloak check is claimed yet.

## API-03: safe invalid OIDC callbacks

- Red: all six invalid callback cases returned generic 500 responses.
- Green: 13 backend tests passed, covering nonce/issuer/audience/expiry/signature/state rejection, safe retry redirect and absent application session. Formatting passed after whitespace-only fixes.
- Identity fixture uses signed RSA tokens through the actual OIDC code-exchange handler; no production authentication replacement is registered.

## API-04: durable opaque session and idle expiry

- Red: a valid OIDC login followed by exactly 30 idle minutes returned 200 instead of 401, including against the real PostgreSQL fixture.
- Green: all 14 API integration tests passed with PostgreSQL and the production authentication handler; format verification passed.
- The browser receives a Secure, HttpOnly opaque cookie; PostgreSQL stores its SHA-256 digest and validated identity. An atomic update enforces the idle boundary. EF migration creates the session table.
- Review caught OIDC claim mapping: the validated subject claim carries its issuer after the raw issuer claim is removed. No tokens or cookie contents are logged. Absolute lifetime, rotation, restart and sign-out get separate behavioral checks next.

## API-05: server-side sign-out

- Added regression evidence for already-present absolute expiry and rotation: activity succeeds until one millisecond before 12 hours, then fails at the boundary; another API instance rejects the replaced cookie and accepts the current cookie. All three lifetime checks passed without production changes.
- Red: sign-out acceptance failed because the authenticated response did not supply its antiforgery token.
- Green: all 17 API tests passed, including acknowledged sign-out followed by rejected access from both the browser and a second API instance replaying its copied cookie. Format verification passed.
- Session reads issue a framework antiforgery token; next increment verifies rejection of missing/invalid tokens and untrusted origins before any mutation. This intermediate commit is not release-ready.
- Reference: [ASP.NET antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

## API-06: protect cookie-authenticated mutations

- Red: seven missing/invalid antiforgery or missing/untrusted-origin cases returned 204 and revoked the session instead of 403.
- Green: all 24 backend integration tests passed. Each rejected mutation leaves its session usable; valid same-origin sign-out still succeeds. Format verification passed after formatting a dictionary initializer.
- Allowed HTTPS browser origins are explicitly configured and validated on startup. Unsafe authenticated API requests require an exact origin match plus the framework cookie/header antiforgery pair before dispatch. The OIDC callback remains protected by its protocol state/nonce validation.

## UI-01: private route, sign-in and sign-out

- Red: Playwright served the blank Angular application and failed on the missing sign-in heading. After adding the guarded route, it failed on the missing sign-out control.
- Green: the complete sign-in, return to My Work, sign-out and browser Back flow passed in Chromium, Firefox and WebKit. Production builds passed for the application and all three sibling libraries; initial bundle was 235.66 kB before transfer compression.
- Composition replaces SESSION_SERVICE with a deterministic mock in the Playwright build. Production uses the HTTP adapter; consumers import only its contract/token. No local/session storage is used. Template, style and component files are separate. My Work currently provides its private page heading; its library contents and upload workflow remain upcoming slices.
- Removed only generated welcome components and their generated scaffold tests. All handwritten behavior tests remain. Exact dependency versions and lockfiles are retained; npm reported no known vulnerabilities.
- References: [Angular guards](https://angular.dev/guide/routing/route-guards), [Angular CLI](https://angular.dev/tools/cli).

## UI-02: recover from identity failure and unsafe destinations

- Red: all five focused Chromium checks failed: missing retry feedback, and four unsafe destinations prevented arrival at My Work.
- Green: all 18 browser acceptance checks passed across Chromium, Firefox and WebKit. The production Angular build passed.
- Failed callbacks display a generic retry message; unsafe or malformed local destinations fall back to My Work. Backend validation remains independently enforced.

## UI-03: navigation focus and sign-in accessibility

- Red: the signed-in destination's main content remained unfocused.
- Green: all 18 identity workflow checks passed with destination focus assertions; 42 additional viewport/browser checks passed with axe A/AA scans, reflow and target-size assertions, and reduced-motion preference. Production build passed.
- Inspected 375x667 and 1440x900 screenshots; content and actions remain visible without horizontal scrolling. Screenshots include the main landmark's keyboard focus indicator.
- This is automated sign-in coverage only. Named screen-reader reviews, actual browser zoom checks and iOS/Android device evidence remain required release gates.

## UI-04: session outage recovery

- Red: controlled session-service failure left the private route blank instead of displaying retry feedback.
- Green: nine focused workflow checks and the remaining 54 browser regressions passed; production build passed. An initial contradictory include/exclude test filter selected no tests; corrected to exclude the nine already-run checks before recording the 54 passing regressions.
- Route query parameters now bind to signal inputs, keeping retry feedback correct when Angular reuses the sign-in component. Retry rechecks the original safe destination without storing private state in browser storage.

## API-07: durable photograph and private preview

- Red: PNG upload returned 404 instead of 201 through the authenticated, antiforgery-protected API.
- Green: all 25 backend integration checks passed, including a real decode, database migration, private filesystem writes and a second API instance reading the same saved record and preview bytes. Format verification passed.
- Owner keys derive from validated issuer and subject. Storage keys are generated identifiers, and media reads authorize the owning photograph before opening files. Persisted full-frame PNG and JPEG preview omit metadata; allowed EXIF extraction remains a separate slice.
- This establishes the PNG save/revisit path. Format/byte/pixel/frame bounds, field normalization, cleanup failure recovery and remaining formats are required next increments before this feature can be released.
- References: [NetVips 3.2.0](https://www.nuget.org/packages/NetVips/3.2.0), [Image API](https://kleisauke.github.io/net-vips/api/NetVips.Image.html). Native libvips is pinned to 8.18.6.

## API-08: upload type, byte and decoded-dimension limits

- Red: seven invalid inputs produced 201 or 500 instead of their specified errors; a separate real 100,010,000-pixel PNG was also incorrectly accepted.
- Green: all 33 API integration checks passed. Every rejected upload leaves zero photograph rows and no retained media files. Format verification passed.
- The server compares content signatures with declared MIME types before decoding, bounds streamed bytes independently of reported length, checks decoded dimensions before rendering, and maps failures to 413/415/422 without decoder internals. Inclusive-boundary, animation and remaining supported-format evidence follows next.

## API-09: all supported formats and inclusive boundaries on the deployment runtime

- Seven Windows checks passed for JPEG/PNG/WebP and inclusive byte/edge/pixel limits. HEIC fixture creation failed before the API call because NetVips.Native omits HEVC; this was a dependency/setup failure, not a recorded behavioral red.
- Replaced the incomplete native runtime with the planned Linux system libvips/libheif codec combination. No acceptance assertion was removed or changed. The complete 41-test suite passed in the Linux acceptance container, including actual HEVC encoding/decoding of a synthetic HEIC file and exact 100-megapixel and 25-MB inputs.
- Added `backend/Test.ps1`; its pinned-package rebuild and all eight format/boundary checks passed under 4 CPUs/8 GiB. Format verification passed. Subsequent backend acceptance uses this command from Windows.
- SDK 10.0.303-noble was not published; the container uses published SDK 10.0.400, pinned by digest. Native packages are libvips 8.15.1-1.1build4 and libheif HEVC plugins 1.17.6-1ubuntu4.8 from Ubuntu's repositories. Local compilation remains on .NET 10. MediatR remains 12.5.0.
- Sources: [prebuilt codec exclusions](https://github.com/libvips/build-win64-mxe), [official SDK images](https://github.com/dotnet/dotnet-docker/blob/main/README.sdk.md), [Ubuntu HEVC decoder](https://packages.ubuntu.com/noble/libheif-plugin-libde265).

## API-10: reject animated WebP and multi-image HEIC

- Red: verified two-page WebP and HEIC fixtures were incorrectly saved as single photographs with 201 responses.
- Green: all 43 container API integration checks passed. Both inputs now return 415 with no record or retained media; all valid still images and boundary inputs remain accepted. Format verification passed.
- Checks the decoder's top-level page count before rendering. Animated PNG needs its own explicit chunk check because the installed PNG decoder treats APNG as a static PNG.

## API-11: reject APNG even with a static-only decoder

- Red: a synthetic two-frame APNG returned 201 because the decoder exposed its static first frame.
- Green: all 44 container API checks passed. A bounded PNG chunk walk rejects animation before decoding while accepting the existing still-image and exact-byte fixtures. Format verification passed.
- Fixture includes valid frame-control sequence numbers, zlib image data and PNG CRCs, following the [APNG specification](https://wiki.mozilla.org/APNG_Specification).

## API-12: preserve allowed capture settings and scrub private EXIF

- Red: the saved response lacked capture metadata, after verifying that the JPEG fixture actually contained private owner/GPS fields.
- Green: all 53 container API checks passed. Camera, lens, aperture, shutter speed, ISO, focal length and capture time persist in an explicit allowlisted value object; GPS, owner and serial fields are absent from returned metadata and both retained image variants. The second API instance retrieves the same settings.
- Added regression evidence for all eight EXIF orientations: full image and preview preserve expected dimensions and the expected quadrant's pixel value. These checks passed without changing the existing orientation pipeline. Format verification passed.
- Capture settings use EF 10 complex JSON mapping; no unfiltered EXIF dictionary or original EXIF blob is persisted. Source: [Npgsql JSON mapping](https://www.npgsql.org/efcore/mapping/json.html).

## API-13: normalize and bound photograph titles

- Red: overlong manual titles were accepted, line endings were not normalized, and filename defaults were not capped. Three existing default/boundary cases already passed.
- Green: all 59 container API checks passed. Exactly 200 Unicode scalar values are accepted, 201 are rejected before files or rows are retained, and defaults are truncated without splitting surrogate pairs. Empty/path-containing filenames receive safe title defaults. Format verification passed.

## API-14: optional critique brief on upload

- Red: both saved/absent brief reads lacked the brief property, and all four invalid-field cases returned 201 instead of 400.
- Green: all 65 existing container API checks passed; four additional boundary checks passed for 2,000-scalar intent/feedback, 100-character genre and each allowed experience (10 brief checks total). Format verification passed.
- Brief values normalize before image processing and persist as an explicit JSON value object. Empty values remain absent; experience is serialized by name. Update/version conflict behavior is the next independent slice.

## API-15: edit and clear briefs with revision conflicts

- Red: all eight new API checks failed because PUT brief returned 404 instead of applying updates or returning field/conflict errors.
- Green: all 77 API checks passed. Normalized edits and clearing survive another API instance; invalid fields preserve the existing brief and revision. Two simultaneous writers across separate instances produce exactly one success and one 409, with the winner retained. Another owner and an absent identifier both receive item_unavailable.
- Revisions start at one, including existing migrated photographs. EF includes the revision in update predicates; concurrency exceptions become safe 409 responses. Formatter and diff checks passed. Source: [EF concurrency handling](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

## API-16: private personal notes

- Red: all six notes checks failed at the missing PUT endpoint; existing foreign detail/image/preview reads already produced safe 404 responses.
- Green: all 83 container API checks passed after the notes implementation and consolidation of revision-checked persistence. Notes normalize line endings, accept exactly 10,000 Unicode scalars, survive another instance and clear to null. Oversized, stale and foreign edits preserve stored values. Formatter verification passed.
- Notes remain a separate field from the brief. Their exclusion from external providers will be verified when those adapters are added.

## API-17: page the private My Work collection

- Red: all eight list checks returned 405 because the GET collection endpoint was absent.
- Green: all 91 container API checks passed. Twenty-five uploaded photographs traverse default 24-item pages across API instances in descending creation time/ascending identifier order, including timestamp ties. Another owner's photograph is excluded. Empty pages succeed and invalid sizes/cursors have field errors. Format verification passed.
- Query uses a bounded keyset cursor and selects only card fields, leaving notes and brief text out of collection payloads. Critique status and reference exclusion fixtures will be extended when those record types are introduced.

## UI-05: browse photographs with honest collection states

- Red: four Chromium acceptance checks found no cards, empty state or retry UI.
- Green: all 75 browser checks passed across Chromium, Firefox and WebKit. The collection appends 24-item pages, distinguishes empty success from failure, preserves existing cards on next-page failure and retries that cursor. The production application and all three library builds pass; the bundle totals 263.90 kB before transfer compression.
- HTTP remains in the api adapter. Domain owns signal state and consumes PHOTOGRAPH_SERVICE; the app composes it with a presentational card. Playwright substitutes the interface with controlled synthetic images and failures. Responsive grid and detail navigation remain the next slices.

## UI-06: responsive photograph grid

- Red: ten larger viewport checks found one column instead of the specified two, three, four or five; the four XS cases already passed.
- Green: all 42 grid/axe/reflow checks passed across three browser engines and 14 viewport sizes. All 12 collection workflow regressions, all 99 independent design-system checks, and the production build passed.
- Added authoritative grid sizing tokens to the design system and mirrored them into Angular. Inspected 375x667 and 1440x900 screenshots: cards, titles, localized dates and header remain legible with the required column counts. This is automated desktop-engine evidence, not a claim of manual device/screen-reader conformance.

## UI-07: open and revisit saved photograph details

- Red: four detail/navigation checks failed at missing card links, the missing route and missing pagination focus. A fifth check then exposed the unexplained empty EXIF section.
- Green: all 132 browser checks passed across Chromium, Firefox and WebKit; the production build and diff checks passed. Details show the full image, normalized brief, available capture settings and separate notes. Missing metadata is explicit. Direct URL reloads perform reads only; unavailable content and temporary failure have distinct states.
- Pagination focuses the first newly added card. Routed pages own routes and back links; domain consumes the photograph token. The controlled library now runs through a Playwright binding so saved fixture state survives page reload without browser storage. No production HTTP adapter is used in Playwright.
- Responsive detail composition and editing are subsequent slices. Tooling-only interruptions while applying the patch were corrected before the clean production build and full regression run.

## UI-08: responsive photograph detail

- Red: the five desktop-width checks found stacked context instead of the required adjacent pane; nine smaller viewports already passed.
- Green: all 57 detail workflow/layout checks, all 99 design-system regressions and the production build passed. Inspected 375x667 and 1440x900 screenshots. The full image retains its aspect ratio, context stacks below 992px and sits alongside above it, and automated accessibility/reflow checks pass.
- The authoritative detail-column token lives in the independent design system and is mirrored into Angular.

## UI-09: explicit personal-note editing

- Red: all three new notes workflows failed because no editable notes control existed.
- Green: 129 relevant browser checks passed across Chromium, Firefox and WebKit, including 14-size notes failure/validation layouts, detail regressions and sign-in. The production build and diff checks passed. Phone/desktop error screenshots were inspected.
- Notes expose unsaved/saving/saved/error states, wait for acknowledgment, preserve failed drafts for retry, normalize saved text, survive reload through the controlled library, clear explicitly, and enforce the 10,000-scalar limit without splitting emoji. The HTTP adapter obtains its antiforgery proof through the session interface; domain holds editor state in signals.
- The existing detail assertions now verify the same saved notes through the editor value. No content assertion was removed. Conflict recovery is the next slice. Reference: [Angular template-driven forms](https://angular.dev/guide/forms/template-driven-forms).

## UI-10: recover from stale notes without losing the draft

- Red: both conflict tests received a generic retry message instead of a conflict/reload workflow.
- Green: all 72 relevant notes, conflict, detail and viewport checks passed. A six-check cross-browser follow-up verified wording that correctly refers to the photograph revision, which can change independently of notes. Production build and diff checks passed.
- Two browser editors share one controlled library: the second retains its text, loads the latest saved notes separately, and only changes storage after an intentional Save. A failed reload preserves the draft and offers retry. The same conflict/review flow passes axe/reflow checks at all 14 viewport sizes.

## UI-11: edit and clear a photograph brief

- Red: six brief checks failed at the absent editor. The first implementation then exposed a stale notes revision after a brief save, preventing an otherwise valid note save.
- Green: all 33 brief, notes and conflict checks passed across Chromium, Firefox and WebKit. Production build and diff checks passed. A build initially overlapped the browser run and temporarily removed shared library output; checks were rerun after the build completed.
- The editor saves normalized optional fields, enforces scalar limits, retains failed edits for retry and restores focus on close. Saving a brief preserves an unsaved note and updates its revision only when the saved note has not changed. Brief conflict recovery, discard confirmation and viewport coverage remain subsequent slices.

## UI-12: recover and review a stale brief

- Red: two conflict checks received a generic retry response; a third could not save an open brief after notes advanced the photograph revision.
- Green: all 42 brief/notes/save/conflict checks passed across three browser engines, followed by the production build and diff check.
- Failed and stale drafts remain editable. Reload presents all latest brief values separately and requires an explicit Save to replace them. Reload failure retains the draft. A notes-only save updates the open brief's revision when its saved brief values still match.

## UI-13: protect unsaved photograph edits

- Red: four checks demonstrated missing navigation/close confirmation and missing unload protection. The clean-editor control already passed.
- Green: all 273 browser checks passed across Chromium, Firefox and WebKit (6.1 minutes), followed by the production build and diff check. Inspected brief-error and discard-dialog screenshots at 375x667 and 1440x900.
- The application page owns the native modal and route guard. Keep editing and Escape retain the draft; Discard allows the requested departure. Closing a brief discards only that editor. A dirty page requests the browser's available unload protection and removes the listener after saving. This event-level check does not claim reliable unload prompts on mobile platforms.
- Existing brief-cancel acceptance now explicitly chooses Discard, preserving its prior saved-content assertion. L2-002.1/.2 and L2-004.3/.4 are checked complete based on the API and browser evidence above; criteria involving unfinished capabilities remain open.
- Sources: [Angular route guards](https://angular.dev/guide/routing/route-guards), [native dialog](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog), [beforeunload behavior and limitations](https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event).

## UI-14: retain keyboard focus through editor recovery

- Red: both latest-value headings remained unfocused after their reload buttons disappeared. The subsequent viewport run also demonstrated Tab leaving the discard dialog after its final button.
- Green: all 60 relevant checks passed across three browser engines, including brief error/validation/pending/conflict/review/discard workflows at all 14 sizes, axe/reflow audits and both dialog tab directions. Production build and diff check passed.
- Latest brief and notes headings receive focus after rendering. The application dialog wraps Tab between its two choices. A strict-template event-type error was corrected using the native keydown event and an explicit Tab check, without type suppression.

## API-18: resolve upload operation keys durably

- Red: eight of eleven new checks failed: concurrent requests created different photographs, conflicting payloads succeeded, retained keys recreated missing records, and key bounds/retention were absent. Three controls already passed.
- Green: all 102 container API checks passed, including concurrent requests across API instances, replay with equivalent normalized fields, payload conflicts, owner isolation, the exact 24-hour boundary and unavailable receipt targets. C# build, format verification and diff checks passed.
- A PostgreSQL transaction-scoped advisory lock and owner/type/key primary key serialize receipt creation. Photograph and receipt commit together; known receipts resolve before decoding or writing media. The upload reader enforces the existing byte bound while computing a streamed digest. Existing acceptance uploads now supply a fresh key through their fixture helper; their behavior assertions are unchanged.
- File cleanup on immediate media-write failure remains; files are retained once database persistence starts so an uncertain commit cannot remove a saved image. Controlled commit-failure coverage and managed orphan cleanup are subsequent work. The receipt port is called directly by the upload handler; a generic MediatR pipeline is unnecessary for this first keyed operation.
- Sources: [PostgreSQL advisory locks](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS), [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), [Npgsql transactions](https://www.npgsql.org/doc/basic-usage.html#transactions).

## API-19: recover from uncertain upload commits

- Red: controlled transient failures immediately before and after the real PostgreSQL commit returned generic 500 responses instead of retryable 503 responses.
- Green: all 104 container API checks passed; format verification and diff checks passed. Retry from a second API instance creates one record after rollback, or resolves the existing identifier and its two media files after commit. The saved preview remains decodable and private diagnostic text is absent from the response.
- Infrastructure translates transient Npgsql failures to the application failure contract. The API returns 503 with a five-second Retry-After. A test-only EF transaction interceptor supplies the controlled fault while persistence remains real PostgreSQL.

## API-20: retry an upload after media storage recovers

- Red: a real filesystem obstacle at the test-owned media root produced a generic 500 response.
- Green: all 105 container API checks passed; format verification and diff checks passed. The failed request returns 503/Retry-After without a photograph. Removing the obstacle and retrying the same key saves one photograph with two readable media files.
- The private image adapter translates write I/O failures to the existing service-unavailable contract. The test restores its explicitly owned temporary path in a finally block.

## API-21: reject extra multipart files

- Red: both duplicate image fields and an additional attachment field were silently accepted as a single successful upload.
- Green: all 107 container API checks passed; format verification and diff checks passed. Extra files return an image field error before decoding, creating a photograph, writing media or reserving a receipt. The corrected single-file request can reuse its key.
- The controller binds the multipart file count; the application validator enforces exactly one file.

## API-22: enforce request size before multipart parsing

- Red: oversized multipart requests, both with and without Content-Length, returned framework 400 errors instead of 413. The 25 MB streamed control already passed.
- Green: all 110 container API checks passed; format verification and diff checks passed. Oversized bodies have the image-too-large contract without a photograph or managed media, and a boundary-valid streamed image succeeds.
- A read-only body wrapper enforces the existing 26 MB whole-request allowance before antiforgery/form parsing. Declared oversize is rejected immediately, and streamed bytes are counted independently. The separate 25 MB image limit remains unchanged. Native server 413 read failures retain the same application error contract.
- Reference: [ASP.NET Core upload buffering and request limits](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0). Private placement and cleanup of framework temporary buffering remain part of deployment/cleanup work.

## API-23: recognize supported image bytes and HEIF brands

- Red: thirteen valid-image cases returned 415 for absent/generic MIME declarations, parameters, or HEIF brands. Three rejection controls already passed. The extended-size fixture first failed automatic format detection; explicit HEIF decoding established its validity before the API failure was recorded.
- Green: all 126 container API checks passed. Format, build (zero warnings/errors), and diff checks passed. Every accepted fixture produces a readable preview; false declarations and unsupported bytes remain rejected.
- MIME parsing compares the base media type and allows absent/generic declarations. HEIF recognition bounds the file-type box, supports its extended size and compatible HEVC still-image brands, and uses the explicit native HEIF loader. Sequence brands remain unsupported.
- Sources: [HEIF brands](https://nokiatech.github.io/heif/technical.html), [HEIC media type](https://www.iana.org/assignments/media-types/image/heic), [NetVips HEIF loader](https://kleisauke.github.io/net-vips/api/NetVips.Image.html).

## UI-15: save a photograph from My Work

- Red: all three upload workflows failed at the missing Upload photograph link.
- Green: all 36 upload, detail and library checks passed across Chromium, Firefox and WebKit. Production build and diff checks passed.
- An application route composes the signal-backed domain form. The API contract accepts a file, optional title/brief and operation key; the HTTP adapter owns multipart serialization and antiforgery headers. Acknowledgment opens the saved detail. Empty defaults and normalized full briefs survive reload, and pending submission cannot create a second request.
- The controlled library now accepts uploads through the injected mock. File validation, progress, retry recovery and unsaved protection are subsequent slices. The dedicated route keeps the full brief usable and gives the application ownership of later navigation dialogs.

## UI-16: validate upload fields before submission

- Red: seven new checks demonstrated missing text limits and empty/oversize/unsupported-file feedback. The 25 MB control already passed.
- Green: all 33 upload and validation checks passed across three browser engines. Production build and diff check passed. Inspected the form at 375x667 and 1440x900.
- Field errors count normalized Unicode scalars, associate messages with their controls, disable invalid submission, and retain the brief while files are corrected. Empty/generic MIME declarations remain eligible for authoritative API byte validation. Browser boundary fixtures test the form's service boundary; native decoding and dimension limits remain covered by API acceptance.

## UI-17: recover upload failures without duplicating photographs

- Red: all eight retry checks failed at generic failure feedback or absent reselection guidance. The acceptance library now retains operation receipts and can lose a committed response.
- Green: all 57 upload/save/validation/retry checks passed across three browser engines, followed by production build and diff checks.
- One operation key lives for the form's lifetime, including file reselection and edits. Unconfirmed saves offer an explicit retry; changed payloads conflict and can be restored to resolve the original photograph. Specific image errors retain the brief. An unreadable file clears the picker and requests reselection; the HTTP adapter checks file readability before submitting.
- The mock hashes actual selected bytes so same-name replacements cannot evade the conflict checks. API acceptance separately establishes durable receipt and media behavior.

## UI-18: report transfer progress without claiming persistence

- Red: all three progress checks failed because no progress indicator existed.
- Green: all 42 progress/retry/save checks passed across Chromium, Firefox and WebKit. Production build and diff checks passed.
- The upload contract reports transferred bytes and an optional total. Domain stores those values in signals. Known totals drive a native progress bar; unknown totals stay indeterminate. Transfer completion retains the saving state until the response, and retries reset previous progress.
- Angular HTTP events and RxJS conversion stay inside the API adapter. Production composition selects XHR for upload progress; the injected mock has a controlled progress event boundary that is removed after completion.
- Reference: [Angular upload progress and XHR configuration](https://angular.dev/guide/http/making-requests#receiving-raw-progress-events).

## UI-19a: share the application discard dialog

- Extracted the existing native dialog and unload listener into an application-owned component for reuse. Existing notes/brief behavior, Escape, focus wrapping and session-aware departure are preserved.
- All 411 browser checks passed (10.2 minutes), including the existing editor and dialog regressions. Production build and diff checks passed. The related upload behavior is committed separately below.

## UI-19b: protect unfinished upload navigation

- Red: seven checks demonstrated missing file/text/failed-draft confirmation, unload protection and in-flight departure behavior. Empty-form departure and explicit sign-out controls already passed.
- Green: all 411 browser checks passed, followed by the production build and diff check. Inspected phone and desktop upload-error and pending-discard screenshots. Independent read-only review by gpt-5.6-sol found no Critical or Required changes; the reviewer did not run additional tests.
- Uploads remain dirty until acknowledged. Keep editing/Escape retain drafts; Discard permits departure. Pending uploads warn that they may finish after leaving. Completion after departure does not navigate back, and acknowledgment during an open prompt opens the saved detail without a second prompt. Guards use the caller's current dirty state so a synchronous saved event need not wait for template input propagation.
- Full upload viewport coverage is the next slice. Native unload checks establish listener behavior, not guarantees about mobile browser prompts.

## UI-20: upload recovery across the viewport matrix

- All 42 added checks passed across fourteen viewports in Chromium, Firefox and WebKit. These extend coverage of previously implemented behavior; the first run was green and no production change was needed.
- Checks cover file/field errors, determinate and indeterminate transfer progress, failed-save recovery, pending-departure warnings, dialog focus wrapping and bounds, control targets, horizontal overflow and axe WCAG checks.
- The preceding full 411-test browser regression and production build passed on the same production code. Diff checks passed. Automated coverage does not replace the outstanding manual device and assistive-technology release reviews.

## API-24: revoke photograph access with a durable deletion operation

- Red: all six new checks failed at the missing DELETE endpoint (405).
- Green: all 132 container API checks passed; format, build (zero warnings/errors), and diff checks passed. The first regression exposed PostgreSQL timestamp precision differences; returning the persisted record fixed repeat-response consistency without changing assertions.
- DELETE /api/photographs/{id}?revision=... removes the owned photograph and records its two media keys atomically. Existing revision concurrency protects concurrent edits; a transaction advisory lock serializes repeated deletion across instances. GET /api/deletions/{id} exposes only the owner's status, never storage keys.
- The operation remains Pending until physical cleanup. Its journal schema is separate from content for later independent restore retention. Worker cleanup, retention and disaster recovery are subsequent slices; this does not claim those guarantees are complete.
- Sources: [EF concurrency on deletion](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [PostgreSQL advisory locks](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS).

## API-25: retry uncertain deletion commits

- Red: controlled failures before and after the real PostgreSQL commit returned generic 500 responses.
- Green: all 134 container API checks passed; formatting and diff checks passed. A second API instance observes the photograph before rollback or its revocation after commit, and retry resolves one durable Pending operation with unavailable content.
- Transient database failures use the existing 503/Retry-After contract without exposing private diagnostics. This proves the live deletion transaction boundary; backup restore and independently durable disaster recovery remain separate gates.

## API-26: finish photograph cleanup in an independent worker

- Red: both acceptance workflows failed because the independent worker executable was absent.
- Green: all 136 container API checks passed, including concurrent worker processes and recovery from a filesystem obstruction after process restart. Format, build (zero warnings/errors), locked restore and diff checks passed. The full suite was rerun successfully after refreshing dependency locks.
- The worker dispatches a scoped MediatR maintenance command each minute by default. PostgreSQL row locks with SKIP LOCKED coordinate bounded manifests across instances. Each file is rechecked against current photograph references; successful removals leave the manifest even when another file fails. Completion clears all storage keys and survives API restart. Filesystem and database failures leave retryable durable work.
- The independent gpt-5.6-sol review found no Critical or Required changes. It did not run extra tests. Direct cleaner database-failure/cancellation injection and intentionally shared-key coverage remain unexecuted; the tested live-photo control has independent files.
- Adding the worker refreshed stale API/test lock entries for the already removed NetVips.Native dependency. No package version was upgraded; Linux distribution HEVC decoding remains verified by the full suite.
- Fairness beyond a full failed batch, overdue alerts, abandoned-media scanning, journal retention and backup restore remain subsequent increments. Reference: [scoped services in .NET background workers](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service).

## API-27: prevent failed cleanup batches from starving later deletions

- Red: a worker repeatedly selected 100 blocked manifests and never completed the healthy deletion behind them before the acceptance deadline.
- Green: all 137 container API checks passed; format and diff checks passed. The test creates the full backlog through the API, retains real filesystem obstructions, and verifies later completion while blocked cleanup remains Pending.
- A persisted last-attempt timestamp rotates retry priority behind unattempted and less recently attempted records. Selection remains bounded and row-locked across worker instances. The migration updates the cleanup index; the public deletion response is unchanged.

## API-28: remove abandoned media without racing uploads

- Red: old unreferenced final/partial files remained; the paused-upload control already passed before the scanner existed.
- Green: both targeted checks and all 139 container API checks passed; format, build (zero warnings/errors), and diff checks passed. A controlled mutation removing the shared upload lock made the safety test fail because both in-flight files disappeared. The lock was restored before the full regression.
- The scanner removes up to 100 eligible files per iteration, with a one-hour grace period (files at the cutoff are eligible). It preserves current references, recent files, unknown names and reparse points. Upload transactions take a shared media lock before their operation lock; the scanner takes its exclusive counterpart before the reference snapshot and file removal.
- Independent gpt-5.6-sol review found no Critical or Required changes, without running additional tests. Explicit enumeration/permission fault and multi-cleaner stress checks remain unexecuted. Framework multipart temporary storage is separate deployment work.

## API-29: retain deletion records for 35 days

- Initial fixture correction: backdating the clock before sign-in created already-expired browser cookies, so setup returned 401. Signing in before advancing the controlled clock corrected setup without production changes.
- Red: the 36-day completed record remained indefinitely. The 34-day and 36-day pending-cleanup controls already passed.
- Green: all 142 container API checks passed; format and diff checks passed. Completed records expire after 35 days, retained records resolve repeated deletion, and incomplete cleanup remains available even beyond retention age. Deleted media access stays unavailable after journal expiry.
- Retention runs before media maintenance so a filesystem outage does not prevent completed-record pruning on the next iteration. Backup retention and preservation/replay of the independent journal are still deployment/restore work.

## API-30: diagnose overdue deletion cleanup

- Red: both below/above-threshold checks lacked the expected structured diagnostics. Each case has an isolated database so an older blocked fixture cannot contaminate the control.
- Green: all 144 container API checks passed; format, build (zero warnings/errors), and diff checks passed. A real storage obstruction at 23 hours produces a structured warning; at 25 hours it also produces an Error alert with backlog count, oldest age, runbook path and a correlated worker run. Captured output excludes the private title, media key and connection string.
- Worker console output is JSON with UTC timestamps and scopes naming the entry point and run. Event 3201 reports cleanup at or beyond 24 hours; event 3202 reports retryable media removal. The operational runbook defines the symptom and safe recovery actions.
- This verifies the local alert signal. Monitoring-stack routing, metrics/tracing export and operational alert delivery remain release work; no external notification was sent. Source: [Microsoft JSON console logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/console-log-formatter).

## UI-21: confirm photograph deletion and observe durable cleanup

- Red: all five new Chromium workflows failed at the missing Delete control.
- Green: all 72 deletion, detail, brief, notes and unsaved-navigation checks passed across Chromium, Firefox and WebKit. Production build passed (382.63 kB raw, 97.91 kB estimated transfer); diff checks passed.
- Named native confirmation preserves drafts on cancellation/failure, prevents duplicate requests, and navigates only after acknowledgment. Explicit deletion bypasses a second unsaved-edits prompt. The owned status URL survives reload and polls Pending operations until Completed.
- The first cross-browser run exposed WebKit returning focus to the notes editor on cancellation. Explicitly focusing the delete trigger before opening the dialog fixed this without changing assertions.
- Inspected confirmation/status screenshots at 375 and 1440 pixels. Independent read-only gpt-5.6-sol review found no Critical or Required changes; it did not run extra tests. Conflict/unavailable recovery, lost responses, and the full keyboard/viewport matrix follow in separate slices.

## UI-22: recover deletion conflicts and uncertain responses

- Red: stale-revision review, failed-review recovery and unavailable-item checks received only the generic retry message. The lost-response control already passed using the retained operation.
- Green: 42 deletion/unsaved-navigation checks and 42 deletion/notes-conflict/brief-conflict checks passed across all three browsers. Production build and diff checks passed.
- A revision conflict removes the destructive action until the latest saved brief and notes have been fetched and displayed. Confirmation then uses that revision; another intervening change conflicts again. Failed reads remain retryable, cancellation preserves the original editor drafts, and unavailable items provide a safe explanation without an endless delete retry.
- A simulated response lost after deletion verifies that retry returns the same operation and creates no duplicate journal entry. This uses the injected mock boundary; real database commit-uncertainty coverage remains in API-25.

## UI-23: protect departure while deletion is unconfirmed

- Red: a clean pending deletion lacked unload protection, and Back from a dirty pending deletion opened a second discard dialog.
- Regression discovery: the first 48-case run passed 47 and exposed an existing WebKit unload timing gap immediately after typing. The run's teardown stalled after reporting all cases and was interrupted. Assertions were preserved; the listener now reads current draft and session state at event dispatch instead of waiting for an effect to install it.
- Green: all 75 deletion, editor-navigation and upload-navigation checks passed across all three engines; production build and diff checks passed. Pending deletion blocks route departure until the bounded request resolves, while acknowledgment clears both pending and dirty protection before status navigation.
- Independent read-only gpt-5.6-sol review of UI-22/23 found no Critical or Required changes, without additional test execution. Synthetic unload events verify handler behavior; native close/reload prompts and sign-out during pending deletion remain outside these specific checks.

## UI-24: recover cleanup status polling

- Red: the initial service-unavailable response did not schedule another status check; four other lifecycle/single-flight controls already passed.
- Green: all 48 deletion/status checks passed across Chromium, Firefox and WebKit; production build and diff checks passed.
- Initial transient server failures now retry on the same five-second cadence as transport failures. Failed refreshes retain the last known Pending result. Completed/unavailable outcomes stop automatic polling, repeated manual checks share the outstanding request, and departure ignores late results and clears polling.
- Polling-stop checks advance the browser clock after observing the terminal state, following [Playwright's clock API](https://playwright.dev/docs/clock). They observe service calls and rendered behavior without inspecting implementation timers.

## UI-25: deletion recovery across the viewport matrix

- Fixture correction: the first target-size check assumed 44 pixels. L2-045.6 requires 24 pixels and the authoritative standard control token is 40 pixels. Corrected that new assertion to the approved criterion without changing production dimensions.
- Red: latest saved details appeared after conflict review without receiving keyboard focus.
- Green: all 42 added viewport checks passed, then the full 543-test Chromium/Firefox/WebKit suite passed in 18.3 minutes. Production build passed (386.15 kB raw, 98.54 kB estimated transfer); diff checks passed.
- Review now focuses its heading after rendering. The matrix covers Escape/cancellation, focus wrapping, pending disabled controls, failed deletion, long saved text, keyboard scrolling to fully visible confirmation/cancel actions, status errors/completion, target sizes, overflow and axe checks.
- Inspected 375- and 1440-pixel confirmation/review/status screenshots. Long phone review content scrolls inside the bounded dialog. These automated checks do not replace the remaining native-device and assistive-technology release review.

## API-31: durable private critique admission

- Red: all six new integration checks failed at the missing admission/status endpoints.
- Green: all six targeted checks and the full 150-test container API suite passed. Format, container build and diff checks passed. The initial API build exposed an action name colliding with ControllerBase.Request; renamed it without suppressing the compiler warning.
- Admission atomically saves an immutable image/brief/EXIF snapshot and operation receipt. Notes are excluded. Owner-scoped status survives an API restart; explicit Demo/Live configuration is required for new work. Deletion locks the photograph, cancels queued work and clears its snapshot in the same transaction.
- Independent read-only gpt-5.6-sol review found no Critical or Required findings. Forced simultaneous admission/deletion and direct persisted-snapshot inspection remain coverage limits. Provider execution, quotas, leases and result publication are later slices. Keyed replay after configuration removal is the next slice.
- Verified the official OpenAI model and Responses structured-output/image-input documentation. The configured Live model defaults to gpt-5.4-mini-2026-03-17; no provider calls or credentials were used.

## API-32: resolve keyed critique replays

- Red: restart replay and a response lost after commit returned 503 when analysis configuration was removed. New invalid-key controls initially expected the wrong error code; corrected them to the established invalid_request contract. Concurrent same-key and separate-owner controls already passed.
- Green: all 158 container API checks passed, including eight new replay cases; format, build and diff checks passed.
- Configuration is evaluated only inside the receipt's new-operation callback. A retained request resolves its original operation despite later brief edits or disabled new admissions. Deletion still makes the photograph unavailable. Concurrent requests across API instances resolve one identifier; changed payload conflicts; failures before/after commit remain safely retryable.

## API-33: deduplicate active analysis and enforce admission limits

- Red: different keys created six equivalent active jobs; changed briefs admitted competing work; seven concurrent requests exceeded the five-job cap.
- Green: all 161 container API checks passed; format, build and diff checks passed. Cross-instance requests reuse one active operation, including explicit Regenerate and notes-only edits. Different active inputs return analysis_active/409. A sixth job returns analysis_limit/429 and Retry-After: 30 while content and equivalent jobs remain accessible; deletion releases a slot and another owner remains independent.
- A per-owner transaction advisory lock serializes admission before locking the photograph. Independent read-only gpt-5.6-sol review found no Critical or Required findings. Admission must remain inside its receipt transaction; active jobs must retain valid non-null input snapshots. Completed-result reuse and worker concurrency remain subsequent slices.

## API-34: execute Demo critiques and save the submitted brief

- Red: the separate worker left an admitted operation Queued. Green: the new process-level API workflow produced a complete typed Demo result, preserved later brief/notes edits, survived API restart and rejected foreign reads.
- Additional red: deleting the completed photograph left its input snapshot in the operation record. Deletion now redacts all matching operation inputs and leases, cancels active states and preserves terminal timestamps/status. The result disappears with its photograph.
- Green: all 162 container API checks passed after that correction; format, builds and diff checks passed. The worker claims with a unique 60-second lease, and publication checks that unexpired lease while atomically saving the result and Succeeded state. Demo guidance is explicitly illustrative and does not claim image assessment or call a provider.
- Independent read-only gpt-5.6-sol review found no Critical or Required findings in the bounded Demo path. Forced claim/publication races remain untested here. Invalid-output validation, retries, lease recovery/renewal/concurrency, completed reuse and Live execution remain subsequent slices; the current worker runs only when explicitly configured Demo.

## API-35: validate critique output before publication

- Red: eight malformed replacements (missing sections/exercise, blank explanations/actions, incorrect priority counts, missing uncertainty and unsupported EXIF) all reached Succeeded. The new validator now rejects those outputs before publication.
- Green: the eight checks passed with one durable five-second retry, then safe terminal failure, retaining the earlier result. An additional valid-second-attempt control passed. Deleting a waiting retry exposed stale scheduling metadata; deletion now clears it, and the canceled operation never calls the provider again.
- All 172 container API checks passed; format, builds and diff checks passed. Timestamp assertions allow PostgreSQL's sub-microsecond truncation while preserving the exact five-second schedule.
- Independent read-only gpt-5.6-sol review found no Critical or Required findings. Validation checks supplied EXIF citations and structural evidence classification, not the visual truth or artistic usefulness of prose. Named real-model evaluation, transport failures/timeouts, corrupted-snapshot recovery and lease races remain separate work.

## API-36: reuse completed critiques and preserve regeneration

- Red: unchanged completed inputs admitted another job. The initial mode-separation control already passed. Green: completed reuse, notes-only edits, changed briefs, returning to an earlier brief, explicit regeneration/active reuse, quota-independent reuse and deletion of cached private content passed.
- Additional red: migrating an existing current critique to the new cache schema admitted duplicate work. The migration now backfills the only recoverable earlier result from the photograph's current critique, matched by owner, resource and operation identity. Historical outputs discarded before this schema cannot be reconstructed.
- All 175 container API checks passed; format, builds and diff checks passed. Reuse prefers the current equivalent result, preserves original generation provenance, and atomically restores an earlier matching result when needed. Regeneration keeps the prior critique visible until success.
- Independent read-only gpt-5.6-sol review found no Critical or Required findings. Forced cache-restore/manual-edit races and corrupted cache hardening remain unexecuted coverage limits. The cache is private and deletion redacts both its input and output.

## API-37: recover interrupted work with lease fencing

- Red: both expired-worker scenarios failed because Running work could not be reclaimed.
- Green: all 177 container API checks passed; format, builds and diff checks passed. Recovery becomes eligible at 60 seconds, assigns a new token and permits one recovery within the three-attempt budget. Both stale publication and stale invalid-result rejection are ineffective. A second interrupted recovery becomes safe terminal failure.
- Independent read-only gpt-5.6-sol review found no Critical or Required findings. Concurrent recovery-claimer stress and corrupted null-expiry rows remain coverage limits. Lease timestamps currently use host clocks; deployment time synchronization remains an operational assumption. Renewal and bounded provider calls follow next.

## API-38: renew ownership during provider work

- Red: both controlled-clock workflows lacked persisted lease renewal. Green: an 80-second provider call renews every 20 seconds, remains unclaimable by another worker, and publishes once; deletion cancels a cooperative call on the next renewal tick. Terminal operations stop receiving renewal updates.
- All 179 container API checks passed; format verification, builds and diff checks passed. The handler stops and awaits renewal before publication/rejection, keeping scoped database operations serialized. Lost ownership cancels the provider token; renewal failures leave durable work recoverable.
- Independent read-only gpt-5.6-sol review found no Critical or Required findings. Database-renewal fault injection and exact completion/tick races remain coverage limits. Non-cooperative provider timeout handling is the next slice.
- Added only the test dependency Microsoft.Extensions.TimeProvider.Testing, pinned to 10.9.0; existing dependency versions are unchanged. Existing timestamp-only fixture clocks remain intact. Sources: [Microsoft controllable time](https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing), [PeriodicTimer](https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer.-ctor?view=net-10.0).
