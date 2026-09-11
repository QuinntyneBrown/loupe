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

