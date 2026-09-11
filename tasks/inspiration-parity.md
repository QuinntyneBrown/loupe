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
