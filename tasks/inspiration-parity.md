# Inspiration mock parity

Branch: `feat/inspiration-mock-parity`. Approved scope includes all destination
pages and live provider validation. Mocks are authoritative; the original video
is historical evidence, not the target. Complete one acceptance case at a time:
Given–When–Then, expected RED, implementation, GREEN, regressions, commit.

## Delivery checklist

- [ ] Reference cards: title/attribution overlay, safe source, named placeholders,
      board actions, keyboard/touch, image proportions and grid.
- [ ] Boards: create, rename, delete, counts, list, memberships, remove/Undo,
      alphabetical picker with inline creation, board view and empty state.
- [ ] Collection: header totals, sidebar, tags/More tags/mobile dialog, clear,
      URL state, pagination, loading, errors, empty and empty-filtered states.
- [ ] Unified save: link/upload, draft import, preview/edit, boards, cancel,
      duplicate, fallback link/image/notes, progress, retry and atomic commit.
- [ ] Reference: full image, metadata/notes, image replacement, deletion,
      photographer link, board picker, suggestions/review/manual tags, related.
- [ ] Photographers: collection/detail, draft page read, editable preview,
      duplicates, fallback, edit/delete/link references, summary states.
- [ ] Search: keyword/meaning, examples, type/boards/tags, URL state,
      complete paginated results and all loading/empty/error states.
- [ ] Navigation: four areas, bottom tabs, slash shortcut, Settings and sign-out.
- [ ] Durable workers: real imports, visual suggestions, grounded summaries,
      local embeddings, indexing generations, retries, stale-result protection.
- [ ] Independent design-system examples/tokens and mirrored frontend tokens.
- [ ] Visual comparison of every mock state, dialogs and breakpoint boundaries.
- [ ] API integration/Chromium acceptance, accessibility and regressions.
- [ ] Live validation with permitted fixtures and existing Azure configuration.
- [ ] Updated demo/evidence, final review, push and PR against main.

## Acceptance additions (L2-012, L2-043, L2-044)

1. Given an owned image reference with attribution and source, when the collection
   is read, then the card receives that attribution and source without a detail
   request; another owner's metadata is never returned.
2. Given that card, when hovered or keyboard-focused, then its title and supplied
   attribution appear together with an Open source page action; activating the
   source opens the saved URL without opener access or a private referrer.
3. Given no attribution/source or preview, when browsed, then the card identifies
   unknown attribution and missing imagery and has no fabricated source link.

## Evidence

Record commands and observed RED/GREEN results here as slices complete. Live
Azure configuration was not found in the planning shell; its availability must
be established without printing credentials. Live quality checks are mandatory.

### Card source and attribution
- RED: API card test failed because `attribution` was absent; both Chromium card
  scenarios failed because the supplied/unknown attribution was absent.
- GREEN: all 7 ListReferences API tests and all 13 Inspiration Chromium tests passed.
- Production Angular build passed (existing initial-bundle warning remains).
- Isolated Chromium screenshot inspected: title/attribution overlay and source
  control render correctly. Collection/header/navigation work remains outstanding.

### Private board creation
- RED: 5 API cases returned 404 for the missing board endpoints.
- GREEN: 12 combined board/reference-list API cases passed, covering private
  persistence across API restart, alphabetical names, NFC/case-insensitive
  duplicates, field validation and authentication.
- Added an additive board/membership migration with owner-matched foreign keys.

### Board editing and membership API
- RED: 5 membership/edit scenarios failed for missing endpoints/fields. An initial
  reused-container run missed newly copied tests; touching copied source forced
  discovery and all 5 failures were confirmed before counting their coverage.
- GREEN: 30 board/list/update integration cases passed. The strengthened pagination
  identity assertion also passed with all 5 membership cases.
- Rename/delete preserve references; membership replacement is atomic, revision
  checked and idempotent. Counts cover the full filtered set. Cursors bind the
  owner, selected board and page size.

### Board navigation and dialogs
- Given private boards, the library exposes counted board views and native modal
  create/rename/delete/picker workflows; cancel preserves saved data, removing a
  membership supports Undo, and deleting a board preserves its references.
- RED: initial board UI scenarios failed on the missing New board control. The
  pagination acceptance case then demonstrated 25 loaded references resetting to
  24 after membership save.
- GREEN: 18 board/Inspiration Chromium cases passed, including duplicate drafts,
  reload, multiple memberships, delete/cancel, Undo, inline creation with failed
  assignment/retry, and preserving pagination and keyboard focus.
- API library totals: RED missing `libraryCount`, GREEN 12 membership/list cases.
- Angular production build passed with the existing bundle warning. Inspected
  desktop, picker and 375px screenshots using cached mock images; four desktop
  columns and two mobile columns render without horizontal page overflow.
- Remaining visual work includes tag-row spacing, the unified save control, and
  the shared shell. These screenshots are intermediate, not parity approval.

### Active manual tags API
- Given an owned reference, valid manual tag changes normalize NFC/case identity,
  preserve notes/media/boards, record manual provenance, and persist on reread.
  Invalid names/categories/limits and stale revisions must leave it unchanged.
- RED: both API scenarios failed on the missing tags endpoint (404).
- GREEN: 27 tag, board-membership, list and metadata-update integration cases passed.
- Added an additive tag migration with owner-matched cascading foreign keys.

### Collection tag filters
- RED: API filtering returned 3 board members instead of 2 tag matches and
  accepted an excessive filter list. Desktop/mobile Chromium cases failed on
  the absent filter controls.
- Fixed an EF translation failure in the tag-facet query: order grouped values
  before constructing response records; captured the exception in the API test.
- GREEN: 16 API filter/tag/membership/list cases and 20 Chromium collection/board/
  filter cases passed. Matching happens before pagination; multiple tags use AND,
  counts remain private, cursor scope includes filters, and unknown tags return
  an empty result. Null tag entries produce a validation response.
- Mobile changes preview the count and apply explicitly; closing discards them.
  Selections survive URL reload. Updated two older read-only request assertions
  to include the newly required tag-facet request without allowing writes.
- Inspected 1440px collection and 375px filter-sheet screenshots; no page overflow.
  Production Angular build passed with the existing initial-bundle warning.

### Reference manual tag input
- Given an owned reference, entering a tag saves it and removing a tag updates
  the active set; rereads and collection filters reflect the saved change.
  A failed save retains the requested change for retry, and dirty tags join the
  existing navigation/unload guard.
- RED: both Chromium scenarios failed on the missing Add a tag input.
- GREEN: 35 tag/metadata/filter/Inspiration cases passed; production build passed.

### Private upload drafts and final save
- RED: all 3 API scenarios failed on the absent draft upload endpoint.
- GREEN: draft privacy/cancel, atomic edited save with boards, repeated final-save
  acknowledgment, invalid-board rollback and stale-preview revision cases passed.
- Combined upload/cleanup regression initially had one cleanup timeout. The exact
  test passed in isolation, then all 18 cases passed together without rebuilding;
  no timeout threshold or assertion was reduced.
- Draft media stays out of the library, remains protected from orphan cleanup
  while active, and transfers to the reference in the final receipt transaction.
  Drafts expire after 24 hours. Link import and the unified dialog remain next.

### Source draft admission and cancellation
- RED: all 3 source-draft cases returned Method not allowed for the missing route.
- GREEN: 17 source/upload draft, legacy import-admission and cleanup cases passed.
- Import creates private queued work without a reference. Cancel terminates work
  and clears its captured source payload. An unconfigured importer leaves an
  explicit manual fallback, and owned normalized duplicates return their existing
  reference without queueing another import or exposing another owner's match.
- The source reader will use [AngleSharp 1.8.1](https://www.nuget.org/packages/AngleSharp/1.8.1)
  for HTML parsing and the applicable rules from [RFC 9309](https://www.rfc-editor.org/rfc/rfc9309.html).
  Network loading stays in the existing restricted HTTP transport.

### Source robots policy
- RED: 11 existing acceptance cases failed because IRobotsPolicy was unregistered.
- GREEN: all 29 robots and restricted-network cases pass, including grouped agent rules, wildcard/end matching, encoded paths, missing policies and bounded failures.
- An oversized-response exception initially escaped the fallback; the exact failure now maps to Unavailable. Source transport bypasses ambient proxies so connection checks apply to the destination.


### Source draft extraction
- RED: 4 API-driven worker cases failed for the missing import handler.
- GREEN: 42 combined draft/worker/robots/network cases pass. Direct, Open Graph and Twitter images create private previews; denied redirects, cancellation during fetch and pages with only arbitrary images preserve explicit fallback behavior.
- Publication checks the operation lease, owner and active draft in one transaction; original URLs remain attached and final fetched URL/time are retained in the operation output.
- Worker hosting, legacy-reference publication and transient retry are the next source-processing slice.


### Source retry scheduling
- RED: the 503 retry case returned Failed instead of Queued.
- GREEN: all 9 source-worker cases pass, including recovery after a temporary failure and termination after three attempts. HTTP client timeouts also enter the bounded retry path.


### Independent source worker
- RED: independent-host acceptance could not compile because ReferenceImportWorker was missing.
- GREEN: 10 worker cases pass, including discovery and publication through a separate service provider. The production worker registers the import handler and runs the configured import pool under the shared durable capacity limit.


### Unified save upload preview
- RED: both Chromium cases failed on the absent Save reference header action.
- GREEN: upload/edit/final-save and cancellation pass. Production build passes with the existing bundle warning; desktop and 375px preview screenshots were inspected without page overflow.
- Regression: 57/58 passed together; the remaining navigation case encountered a formatter-triggered dev-server reload and passed unchanged on a stable server. An earlier pair of simultaneous suites collided in their shared trace directory; subsequent suites run sequentially.
- Legacy upload/link tests now open their retained direct routes. Their read-only call allowances include the required tag facet read, with write restrictions preserved.
- Link import, board selection, progress/error states and final visual parity remain in the next dialog slices.


### Unified source import dialog
- RED: 4 link workflow cases failed on the missing From a link radio.
- GREEN: all 24 dialog/board/collection Chromium cases pass. Link imports expose editable previews, explicit fallback notes, duplicate detection and cancel without late reopening. Production build passes with the existing initial-bundle warning.
- Azure CLI is authenticated and lists the existing qbs-ai-ynal4ns37wb6a resource in rg-qbs-prod. No keys were printed; deployment discovery is in progress for the approved live validation.


### Save preview boards, Back and fallback image
- RED: 3 cases failed on missing board selection, Back and Add an image controls.
- GREEN: 9 workflow cases and 3 viewport accessibility cases pass. Current-board selection, inline creation, Back cleanup and retaining fallback source/title/notes are covered. Production build passes with the unchanged budget warning.
- The expanded 375px preview was visually inspected. Picker borders, spacing and typography follow mock CSS. The mock's small optional/count text uses a 3.37:1 color; these labels use the existing secondary-text token to satisfy the accepted accessibility bar.
- Existing Azure deployment discovery found photo-vision running gpt-4.1-mini (2025-04-14), provisioned successfully. No credentials have been printed.


### Draft retry, progress and cancellation races
- RED: cancellation left one preview behind when an image response overlapped Cancel; upload progress was missing; a final-save duplicate closed the dialog.
- GREEN: all 16 dialog cases pass. Both overlapping previews are discarded, transfer progress remains separate from acknowledgment, late duplicates show Already saved, and a lost save response retries without duplicating its reference or new board.


### Draft navigation protection
- RED: an edited preview did not prevent unload, and browser Back bypassed the unsaved dialog.
- GREEN: all 23 dialog/board cases pass. Keep editing restores the preview, Discard cancels it, and leaving an admitted final Save warns while ignoring its late response. The retained session guard allows sign-out to complete.


### Collection empty and error parity
- RED: 3 cases exposed the old empty/error copy and missing Save entry points.
- GREEN: all 39 collection/board/filter/draft Chromium cases pass. Empty library and empty board actions open the unified dialog and restore their initiating control; retries preserve the existing focus behavior.
- Empty/filter/error copy follows the mocks. Mobile grid spacing and the empty tag-host gap were corrected.


### Board pagination through Remove and Undo
- RED: removing one of 48 loaded board references reset the collection to 24.
- GREEN: all 21 board/filter/collection cases pass. Remove keeps 47 displayed entries, Undo restores 48 and keyboard focus, and the existing cursor loads the remaining entries without duplication.
- Membership success updates the displayed collection and board counts without resetting its pages; restoration respects the current board and tags.


### Reference image replacement API
- Given an owned reference, replacing its image preserves metadata, boards and tags; stale/foreign/unsupported replacements leave it unchanged, and retrying one operation does not apply it twice.
- RED: both cases returned 405 because replacement was absent.
- GREEN: all 42 replacement/upload/metadata/membership/source-worker cases pass. Separate image revision invalidates image and preview URLs without changing them for editorial updates.
- The invalid-byte assertion follows the existing 415 unsupported-media contract; successful decoding and unchanged saved content remain asserted. Initial regression caught metadata URL churn and SQL identifier quoting; both were corrected.

### Reference replacement dialog
- RED: all 4 Chromium cases failed on the missing More actions control.
- GREEN: 24 replacement/metadata/tag cases pass. Save is explicit, Cancel writes nothing, unsupported files are rejected, failed saves retain the file, and stale revisions require latest-reference review.
- Desktop and 375px dialog screenshots were inspected; Axe and overflow checks pass at both sizes. The drop area uses the mock's spacing, type, border and radius tokens.

### Reference deletion API
- Given an owned reference, deletion removes its tags and board memberships, revokes media access, cancels source work and leaves boards/other references intact. Stale and foreign requests leave it unchanged, and a retry after restart returns its deletion journal entry.
- RED: all 3 deletion cases returned 405.
- GREEN: 19 reference deletion/replacement/import/membership cases and 10 existing photograph deletion/cleanup regressions pass.

### Reference deletion dialog
- RED: all 3 Chromium cases failed on missing Delete reference.
- GREEN: 36 reference deletion/image/metadata and photograph-deletion regressions pass. Cancel restores menu focus, explicit Delete returns to Inspiration with confirmation, failures retry and stale revisions require review.
- Desktop and 375px screenshots, Axe and overflow checks pass. The dialog follows the mock confirmation copy and Cancel-first focus.
- A local test-runner startup raced a library build; the ignored parity config now uses the explicitly managed 4218 server without automatic startup. Tests ran after the server stabilized.

### Reference board links
- RED: the detail workflow failed on missing Add to boards.
- GREEN: all 14 detail-board/board/deletion/replacement Chromium cases pass. The existing picker supports inline creation, returns focus to its trigger and updates the named board links; each link opens the matching board collection.

### Independent description and notes API
- RED: all 3 text cases failed on absent description/notes endpoints.
- GREEN: 23 text/metadata/replacement/deletion/tag API cases pass. Description and notes save independently with revision checks, description provenance is manual, clearing remains absent, and the 4,000/10,000 character limits are enforced.

### Inline description and notes editors
- RED: both Chromium cases failed on the missing inline text fields.
- GREEN: all 27 text/metadata/image/tag/board-detail cases pass. Independent explicit saves, persistence, clearing, draft retention, latest-value review and unload protection are covered.
- Notes assertions now read the editable textarea's value instead of the retired read-only paragraph. Both editors use the mock's labeled field, Save footer and status indicator.

### Visual-analysis admission
- RED: both API cases failed on absent analysis routes.
- GREEN: 17 analysis/import/text/deletion cases pass. An owned image admits one durable owner-scoped operation; keyed retry and equivalent active requests reuse it, current state survives restart, and active metadata is unchanged.
- Link-only, stale, foreign and unconfigured requests are rejected. The durable input contains image keys and image revision, never personal notes.

### Visual-analysis invalidation
- RED: both replacement/deletion cases left the old analysis Queued.
- GREEN: all 9 analysis-admission/replacement/deletion cases pass. Replacement serializes with admission, cancels prior work and clears its current pointer; deletion cancels analysis and clears durable input/output.
- Provider implementation reference checked against official OpenAI [image inputs](https://developers.openai.com/api/docs/guides/images-vision) and [structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs). The existing Azure deployment remains the configured target.

### Visual suggestions publication and provider
- RED: the API integration workflow reached its admitted operation but no worker handler was registered.
- GREEN: 20 visual-analysis/critique-provider/execution cases and 15 critique validation/timeout/renewal regressions pass.
- The Azure Responses adapter sends the validated preview with a strict description/tag schema and excludes notes. Publication retains editorial fields and stores private, timestamped suggestions separately, guarded by the lease and image revision.
- Retry handling is shared with critiques, preserving the existing critique messages and bounded backoff. Worker-host registration, review actions, additional adversarial cases and live evaluation remain separate slices.

### Shared image-analysis worker
- RED: the host left reference analysis queued; malformed-output and canceled-publication checks already passed. A queued job from the failing host test also explained the follow-on publication failure.
- GREEN: all 12 reference-worker, shared-capacity and critique-execution cases pass. The renamed AnalysisWorker alternates reference and critique work within its existing pool and database-enforced capacity. Invalid outputs retry once; replaced/deleted image results cannot publish.


### Individual suggestion review
- RED: all 3 API review cases returned 405.
- GREEN: 16 review/worker/text/tag checks pass. Accepted and edited values record provenance; dismissed values stay out of active metadata; reviews persist after restart. Existing tag spelling/provenance survives duplicate acceptance, the 50-tag limit retains pending suggestions, and owner/revision/generation checks protect writes.


### Bulk suggestion review and Undo
- RED: all 3 bulk cases rejected the unsupported all target.
- GREEN: all 19 review/worker/text/replacement cases pass. Bulk decisions are atomic, including tag-limit failure. Undo restores the pre-review description, provenance, active tags and pending states, and refuses intervening revisions. New image analysis invalidates old Undo state.


### Suggestions panel
- RED: all 3 Chromium workflows failed on the absent AI suggestions region.
- GREEN: 25 suggestions/metadata/tags/text cases pass. The mock-based panel separates pending output, edits/accepts descriptions, reviews tags individually or in bulk, restores via Undo, and polls background analysis while notes remain editable. Saved review state survives reload and failed decisions retry without claiming success.
- Contracts and mock bindings remain interface-driven. Tag editing, additional stale/error states and visual/accessibility audit follow in the next slice.


### Suggested-tag editing and accessibility
- RED: tag editing failed on the absent Edit tag control. Existing stale-review recovery and all three viewport accessibility checks passed.
- GREEN: 19 suggestions/text/tags/replacement/deletion Chromium cases pass. Tag names and categories edit before acceptance, failed drafts remain visible, and success records edited-AI provenance and restores heading focus.
- Axe/overflow pass at 1440, 768 and 375px. Desktop suggestions and mobile tag editor screenshots inspected. Protection against overlapping review actions follows.


### Overlapping review protection
- RED: Accept description stayed enabled during a tag draft.
- GREEN: all 9 suggestion Chromium cases pass. An open tag draft disables description/bulk review and Undo; cancel restores those actions without losing previously reviewed metadata.


### Automatic analysis after image saves
- RED: all 3 automatic-analysis cases returned no current operation after Save.
- GREEN: 25 of the initial 26 checks passed; the remaining assertion still expected no analysis after replacement. It now verifies automatic replacement admission and explicit-request deduplication. All 29 automatic/admission/worker/import-admission/lease cases then passed.
- Validated uploads, final draft saves and replacements enqueue once in the save transaction when the provider is configured. Queue saturation records a retryable analysis failure without losing the saved image. Draft preview alone never saves a reference; keyed retries reuse the operation.


### Saved-source imports and durable provenance
- RED: saved-reference jobs were not claimed, and a committed draft did not retain source provenance.
- GREEN: 18 of 19 initial checks passed; the 25-hour persistence test then correctly encountered session expiry. With a fresh sign-in for that persistence read, all 13 saved-source/draft-worker/worker-host cases pass.
- Workers process both saved links and drafts. Saved-link imports fill a missing image, preserve user metadata, reject changed source/image snapshots and queue visual analysis. Requested/fetched URL, time and extracted title/attribution persist independently of the draft lifetime.


### Declared source restrictions
- RED: three structured-paywall fixtures fetched their advertised image; the password case already passed.
- GREEN: 34 source-worker and robots cases pass. JSON-LD and microdata access declarations are checked before image candidates; restricted pages retain the manual fallback.
- Checked the access flag against [Schema.org isAccessibleForFree](https://schema.org/isAccessibleForFree).


### Active-tag editing
- RED: both new workflows failed on missing active-tag edit controls.
- GREEN: 15 tag/suggestion/text Chromium cases pass. Renaming and optional categorization preserve unchanged tags' provenance. Failed and stale edits retain the draft and require explicit latest-tag review before retry. The saved tag status uses an accessible success token; the edited-tag page passes Axe.


### Photographer bookmark foundation
- RED: eight save/read acceptance cases failed on absent endpoints.
- GREEN: 26 bookmark/reference URL/tag API cases pass. Private manual bookmarks persist across restart, validate metadata and public URL syntax, and deduplicate normalized URLs under concurrent saves without fetching the portfolio.


### Photographer metadata editing
- RED: both edit workflows failed with MethodNotAllowed.
- GREEN: all 10 save/edit API cases pass. Revision checks preserve concurrent edits, portfolio URL conflicts leave both records intact, and unchanged tag provenance is retained.


### Photographer collection pagination
- RED: four list cases failed on the absent GET collection endpoint.
- GREEN: 14 save/edit/list cases pass. The frozen-clock fixture initially expected insertion order; it now explicitly tests both created-time order and the required ID tie-breaker. All 25 records appear once, with private totals and owner/page-size scoped cursors.


### Reference photographer association
- RED: both attribution variants failed on the absent link endpoint; the foreign-item case already returned 404.
- GREEN: 36 photographer/reference metadata/suggestion cases pass. Link changes are private, revision checked, and preserve textual attribution through rename, replacement and unlink. The database enforces owner-matched associations.


### Photographer deletion
- RED: deletion failed on the absent endpoint.
- GREEN: 27 photographer and content-deletion API checks pass. Deletion immediately removes the bookmark from collection/detail, unlinks references without changing credit/source/notes, increments their revisions, and reuses the private deletion record. Replaying the original save key cannot resurrect the bookmark.


### Photographer linked-reference reads
- RED: the linked-reference collection returned 404.
- GREEN: all 19 photographer API cases pass. Linked references page 24 at a time with private scoped cursors. Collection cards include full counts and at most three newest references through bounded projections; unlinking immediately changes the result set.


### Photographer collection UI
- Initial RED encountered the absent page; after implementation the new page object's full navigation reset the in-memory session. Correcting its sign-in flow exposed an additional real RED: portfolio links failed target-size checks.
- GREEN: five collection Chromium cases pass at 1440, 768 and 375px, including Axe, paging and failure recovery. All 13 Inspiration regressions passed during the first implementation run. Portfolio hit areas now meet the minimum target size.
- Desktop/mobile mock screenshots and implementation screenshots inspected. The read-only collection is in place; Add photographer, destination detail and shared navigation follow in later slices.


### Photographer collection focus recovery
- RED: both retry flows left the new card/empty heading unfocused.
- GREEN: all five collection cases pass. Explicit pagination/retry now focuses the first added card or recovered empty heading after rendering, without moving focus on initial load.


### Photographer read drafts
- RED: private/duplicate draft cases returned 404. After correcting the unconfigured fixture to null (the supported configuration), its fallback case also failed on 404.
- GREEN: 26 draft/bookmark/abandoned-media API cases pass. Page-read admission creates no bookmark, deduplicates operation keys, returns existing portfolios before admission, hides foreign/expired/canceled drafts, and supports repeated cancellation. Expired drafts join existing cleanup.


### Bounded photographer page reading
- RED: five allowed/restricted/Unicode worker cases remained Queued. The implemented pipeline passed 22 source/draft cases.
- Review then found the standalone worker's restricted handler registration needed updating. A process-level acceptance case reproduced Queued with the registration absent; adding the handler made all 23 source/draft/standalone-worker cases pass.
- The shared source pool reads one allowed HTML page, ignores navigation/scripts/hidden sections, captures title/description/keywords and at most 20,000 main-text Unicode scalars, and never follows gallery links. Robots/password/paywall failures remain manual fallbacks. Captured provenance stays private on the draft.


### Final photographer draft saves and source generations
- RED: both final-save cases failed on the absent endpoint.
- GREEN: 29 bookmark/draft/source-worker API checks pass. Final Save validates the whole draft and persists one bookmark under keyed retries; pending/stale drafts cannot commit. Captured source survives draft expiry. Equivalent URL spellings retain its generation, while a changed portfolio URL marks the retained capture as previous without replacing manual notes or summary.


### Add photographer dialog
- RED: five Chromium cases failed on the missing Add photographer control.
- GREEN: all ten Add/collection cases pass. Reading creates a private draft; preview supports edits, tags and final Save. Cancel does not create a bookmark, blocked pages permit manual entry, existing portfolios are detected, and lost save responses retry the retained operation key.

