# Search library text with combined filters

## Overview

**Keyword search** — normalized substring matching across saved library fields — finds references and photographer bookmarks. A **filter group** — selected tags, boards, or item type — narrows results alongside the query. My Work photographs and critiques never enter this searchable library.

This is a proposed design for the defined requirements. Existing files are illustrative HTML mockups; the repository contains no corresponding production implementation.

## Description

`SearchPage` lives in `frontend/projects/loupe` and owns routing and dialogs. `SearchResults` lives in `frontend/projects/domain` and consumes `ISearchService` through `SEARCH_SERVICE`. The contract and token share `search.service.contract.ts` in `frontend/projects/api`. `SearchService` is the separate production HTTP adapter. Composition substitutes a mock implementation under Playwright. State uses signals, and HTTP and observable conversion stay inside the adapter. Presentational controls in `components` consume inputs and emit outputs. Component class, template, and styles occupy separate files.

`SearchController` lives in `backend/src/Loupe.Api/Controllers` under `Loupe.Api.Controllers`. It binds requests, dispatches MediatR **12.5.0**, and returns results. Requests, handlers, and validators live in the matching feature namespace in `Loupe.Application`. `SearchFilter` lives in `Loupe.Domain`; the domain references no other project. `ILibraryStore` and capability ports belong to Application. Infrastructure implements persistence and external adapters. Each type occupies its own named file.

`SearchPage` owns query, mode, tag, board, and type parameters in the URL. `SearchResults` injects `SEARCH_SERVICE`; the API implementation holds result and request-state signals. Keyword is the default mode. Browser Back, Forward, and reload restore the same parameters; clearing filters preserves query and mode. Each response is applied only if its request identity still matches the active query.

`KeywordSearchQueryHandler` normalizes text with NFC and invariant case-insensitive matching, splits query on whitespace, and requires every token to occur in at least one eligible field. Fields are title/name, active description/summary, notes, active tags, textual attribution, linked photographer name, and source/portfolio hostname. Board names and pending AI suggestions are excluded. A blank query browses filtered items; over 500 Unicode scalar values returns 400 before search execution.

`SearchFilterValidator` permits up to ten active tags and ten board identifiers, plus All, References, or Photographers. Tags combine with AND and board membership with OR. Query, tag group, board group, and type combine with AND. Any board selection excludes photographer bookmarks. Photographers plus a board therefore returns explained empty results; neither filter is ignored. Unknown valid tags yield no matches. Unknown or foreign boards return 404 before querying results.

`ILibrarySearchReader` reads current owner-scoped, nondeleted relational state and joins active tags and linked photographer names. Existence predicates for boards avoid duplicate references across selected boards. Matching rows and count use the same snapshot and predicate. Results follow descending creation time then ascending identifier, with shared cursor limits. Each result includes kind, title/name, preview or placeholder, and protected original-source action.

The UI separates successful No results from retryable service failure. Empty results offer query editing or filter clearing. Filters feed the same predicate into Meaning search before ranking and pagination. A deleted board in a saved URL offers removal without silently broadening the query.

The following contract surface names the proposed operations. Background commands run in the worker after a separate admission request; local evaluation commands run in the evaluation project.

| Application request | Entry point | Input |
| --- | --- | --- |
| `KeywordSearchQuery` | `GET /api/search?mode=keyword` | `query?, tags?, boardIds?, type?, cursor?, pageSize?` |
| `ValidateSearchFiltersQuery` | `GET /api/search?mode={keyword|meaning}` | `tags[0..10], boardIds[0..10], type` |

All operations inherit [shared contracts and open dependencies](../../README.md). Owner identity comes from authenticated server context, never from trusted client input. Mutations validate complete input before side effects and use conditional writes. API errors use the shared status mapping in [L2.md](../../../specs/L2.md#shared-acceptance-definitions). Visual implementation uses mirrored `--lp-` tokens and the specification's viewport matrix.

Implementation proceeds one behavior at a time using the linked Given-When-Then criteria. Each acceptance test first fails for its expected missing behavior. API integration tests cover persistence and failures. Playwright tests use one page object per screen and injected service mocks. Real-provider release checks supplement deterministic fixtures where applicable. Each acceptance file identifies L2 coverage and each test names its criterion. Regression checks pass before the next behavior starts; no architecture tests are introduced.

## Requirements

The table preserves source wording verbatim, including its use of “must”. Quoted requirements are the sole exception to the design prose's shall/should/may convention. Each linked source section also supplies the acceptance criteria; deployment choices and unexecuted release evidence remain explicitly identified.

| L2 ID | Refines (L1) | Requirement |
| --- | --- | --- |
| `L2-024` | `L1-007` | Search must offer explicit Keyword and Meaning modes, with Keyword the default. Keyword matching must be case-insensitive after Unicode NFC normalization, split the query on whitespace, and require every token to occur as a substring in at least one eligible field. Eligible fields are title/name, active description or summary, notes, active tags, textual attribution, linked photographer name, and source/portfolio hostname. Board names require the board filter instead of text matching. My Work and critiques are excluded. |
| `L2-025` | `L1-007` | Both search modes must accept up to 10 selected active tags, up to 10 board IDs, and item type All, References, or Photographers. Tags combine with AND; boards combine with OR; these groups, query, and item type combine with AND. Selecting any board excludes photographer bookmarks because only references belong to boards. |

Acceptance criteria: [L2-024](../../../specs/L2.md#l2-024-search-saved-library-text-by-keyword), [L2-025](../../../specs/L2.md#l2-025-combine-tag-board-and-type-filters).

## Diagrams

The context view places this capability within the owner's private Loupe library. External services appear only where the slice uses provider or source content.

![Context: Search library text with combined filters](diagrams/c4-context.png)

The container view separates Angular interaction, the .NET API, and authoritative persistence. Durable background work appears where processing continues after admission.

![Containers: Search library text with combined filters](diagrams/c4-container.png)

The component view shows dispatch into Application handlers and inward-defined ports. Infrastructure supplies the persistence and provider implementations.

![Components: Search library text with combined filters](diagrams/c4-component.png)

The class view names the proposed state, requests, handlers, and interface-bound frontend service. Typed associations and dependencies show which element owns behavior.

![Classes: Search library text with combined filters](diagrams/class-structure.png)

The search saved library text by keyword sequence traces `L2-024` through its enforcing operations. The accompanying Description defines state changes and significant recovery paths.

![L2-024: Search saved library text by keyword](diagrams/sequence-024.png)

The combine tag, board, and type filters sequence traces `L2-025` through its enforcing operations. The accompanying Description defines state changes and significant recovery paths.

![L2-025: Combine tag, board, and type filters](diagrams/sequence-025.png)
