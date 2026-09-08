# Loupe detailed designs

## Overview

Loupe supports private photography practice and a searchable inspiration library. This design tree covers every defined requirement in `docs/specs/L2.md`: **L2-001 through L2-052**, refining L1-001 through L1-013. Every defined L2 statement has a feature page, its exact source wording, and an L1 parent link.

The repository currently contains requirements and illustrative HTML mockups, not an Angular or .NET implementation. Names and contracts in these designs are proposed. The thirteen subsystem names follow capability groupings in the flat specifications: My Work, critique, inspiration, metadata, boards, photographers, search, persistence, processing, security, experience, operations, and design system.

## Description

**Architecture.** One .NET API and a worker host share Application and Domain projects. The worker uses Microsoft.Extensions hosting, dependency injection, Options, and Configuration. A relational store holds authoritative owned records and durable work intents. Immutable private image objects remain behind owner-authorized access. Persistence engine, image adapter, AI providers, embedding model, and deployment identities are `<TO SUPPLY>`. They remain explicit ports; no particular vendor integration is claimed.

Live embeddings execute inside the trusted deployment through `LocalEmbeddingProvider`. This preserves notes in semantic input while keeping them out of external AI requests under L2-041. Query and item inference use one compatible real model/version; exact model selection remains subject to the specified relevance and capacity gates.

`backend/src/Loupe.Domain` contains entities and value objects with no project dependencies. `Loupe.Application` contains vertical feature requests, handlers, validators, and ports. `Loupe.Infrastructure` implements those ports. `Loupe.Api` and `Loupe.Worker` provide composition and hosting. `Loupe.Evaluation` is another project under `backend/src`, not a second backend root. Every type has its own file and matching folder namespace. Controllers bind, dispatch MediatR 12.5.0, and return. Authentication middleware and Application authorization handle policy; controllers contain no business logic.

`frontend/projects/loupe`, `domain`, `api`, and `components` are sibling projects. Routed pages, guards, and dialogs belong to the application. Domain components inject interface tokens declared with contracts in `api`; only composition references concrete HTTP adapters. The `components` library is presentational and imports no other project. Each Angular component has separate class, template, and style files. Signals hold state; HTTP and observable conversion remain inside API implementations. Browser routing owns navigable search state, and request identities prevent stale responses replacing newer results.

The mockup token prefix is `--lp-`. Its token file is a seed, not an independent design-system deliverable. `design-system/` becomes the authoritative token source with its own package, tests, build, and deployment, as directed by `AGENTS.md`. Frontend styles mirror and read these tokens. The mockup breakpoints are illustrative; the L2 viewport matrix and the 768-pixel comparison rule take precedence. The [independent reference design](design-system/publish-visual-reference/README.md) covers L2-051 and L2-052.

**Shared request contracts.** Server context supplies owner identity using configured OpenID Connect authentication; no request body can select another owner. Provisioned users, sign-in, sign-out, expiry, and private libraries are the stated baseline. The [session design](security/access-private-library/README.md) uses server-held sessions and opaque Secure/HttpOnly cookies, with 30-minute idle and 12-hour absolute expiry. Identity-provider deployment values remain `<TO SUPPLY>`. Owned queries exclude deleted rows and return the same 404 for absent and foreign identifiers. Image reads authorize the owning record before resolving its object key.

Application validators trim input, normalize line endings to LF, and count Unicode scalar values. Titles, names, and attribution allow 1–200; board names 1–80; tags 1–50; genre 1–100; description/summary 0–4,000; intent and requested feedback 0–2,000 each; notes 0–10,000; query 0–500; URLs 1–2,048. Optional emptiness becomes absence. Active tags are capped at 50. Tag and board uniqueness use NFC and invariant case-insensitive comparison with display spelling retained. Plain text is rendered as text rather than HTML.

`ApiProblemMapper` maps invalid fields to 400, oversized bodies to 413, unsupported/falsely declared media to 415, and corrupt or excessive-dimension images to 422. Unauthenticated requests return 401; unavailable owned resources return 404; stale edits or conflicting request-key reuse return 409. Quotas return 429 and temporary unavailability 503, each with Retry-After. The handler validates before writing and commits synchronous state mutations atomically. The UI retains non-file input until navigation, cancellation, or sign-out. Conditional mutations carry a `version` value; conflicts retain attempted text and offer Reload latest before intentional resubmission under L2-030. Cookie-authenticated writes also reject invalid antiforgery/origin checks with 403 under L2-039.

`CursorPage<T>` defaults to 24 and caps at 100. Collection keyset order is creation time descending then identifier ascending. Board pickers order by invariant case-insensitive ordinal name then identifier. Cursor validation binds owner, query/filter identity, and sort parameters; malformed values return 400. Semantic cursors additionally bind a generation and return 409 when that generation changes. Exact cross-request stability is guaranteed for unchanged datasets, as specified. Dates persist in UTC and display in local time.

**Shared consistency contracts.** Owner-scoped keys and foreign-key checks prevent cross-owner relationships. Photograph and reference records are distinct. A reference has zero or one photographer link, independent textual attribution, and zero or more board memberships. Board deletion removes memberships only. Notes never belong to an AI write set.

`ILibraryStore` denotes feature-specific queries and transactional write operations implemented by Infrastructure, not an unrestricted table gateway. `ICurrentOwner` supplies caller identity; workers use the admitted owner's immutable context. Expected versions protect manual edits. Image and source revisions protect derived output. A transactional outbox stores work intent alongside source changes, so a process restart does not lose acknowledged work. Leases and conditional publication tolerate duplicate delivery without duplicate current output. The [processing designs](processing/track-and-retry-work/README.md) define five states, five-second status visibility, 60-second lease expiry, 120-second provider timeout, and bounded retries. Keys persist 24 hours. Admission permits five requested jobs per owner, two simultaneous provider calls per owner, and four deployment-wide; maintenance bypasses only the requested-job admission cap.

Image writes stage immutable validated objects before committing record pointers. Failed staging exposes no saved item; failed database commit leaves cleanup candidates. Replacements switch pointers atomically and retire old objects only after commit. Semantic writes stage vectors and publish a matching source revision only if the target remains current. Authoritative state gates all search rows and counts. The [deletion design](persistence/delete-content/README.md) revokes access on acknowledgment, completes healthy cleanup within 24 hours, retains deletion records 35 days, and expires backups by day 30. Restore replays deletions before readiness.

**Implementation sequence.** Each feature's source acceptance criteria supply its Given-When-Then slices. Implementation first writes and runs the corresponding failing acceptance test, then adds only production behavior required for that slice. Refactoring and relevant regression checks finish before the next slice. Backend tests use API integration boundaries. Frontend tests live under `e2e/specs` and call screen page objects under `e2e/page-objects`; selectors stay in the page objects. Angular composition binds mocks so frontend acceptance tests never reach real adapters. Controlled provider and clock fixtures establish deterministic behavior; recorded real-provider evaluations establish integration and content quality. No application implementation or behavioral test execution is claimed by these design artifacts.

Implementation follows the source acceptance-delivery order: first an accessible primitive in the independent design system; then identity/private library and durable upload; critique and comparison; reference import; metadata review; boards; photographers; keyword search; semantic indexing and retrieval; complete deletion/restore; release evaluations. Security, accessibility, persistence, and deletion apply from each feature's first relevant increment. Each acceptance file identifies L2 coverage and each test its criterion. Tests do not parse specifications for traceability.

## Requirements

Feature pages quote L2 statements exactly and pair each with its L1 parent. Source statements use “must”; retaining that wording takes precedence over converting quoted requirements to house-style “shall”. Original requirements are unchanged. Every feature also inherits the shared definitions in [L2.md](../specs/L2.md#shared-acceptance-definitions).

| Subsystem | Feature | Defined L2 coverage |
| --- | --- | --- |
| my-work | [Save a photograph and its brief](my-work/save-photograph/README.md) | `L2-001`, `L2-002` |
| my-work | [Revisit photographs and keep notes](my-work/revisit-photograph/README.md) | `L2-003`, `L2-004` |
| my-work | [Compare two saved attempts](my-work/compare-attempts/README.md) | `L2-005` |
| critique | [Request or replace a critique](critique/request-critique/README.md) | `L2-008` |
| critique | [Produce and evaluate an evidence-based critique](critique/produce-critique/README.md) | `L2-006`, `L2-007` |
| inspiration | [Save or replace a reference image](inspiration/save-reference-image/README.md) | `L2-009`, `L2-013` |
| inspiration | [Import a source URL with manual fallback](inspiration/import-reference/README.md) | `L2-010`, `L2-011` |
| inspiration | [Browse and edit saved references](inspiration/browse-references/README.md) | `L2-012` |
| metadata | [Generate visual metadata suggestions](metadata/generate-suggestions/README.md) | `L2-014` |
| metadata | [Review suggestions and maintain active metadata](metadata/review-metadata/README.md) | `L2-015`, `L2-016` |
| boards | [Organize references with private boards](boards/organize-references/README.md) | `L2-017`, `L2-018`, `L2-019` |
| photographers | [Save, edit, and browse photographer bookmarks](photographers/maintain-bookmarks/README.md) | `L2-020`, `L2-023` |
| photographers | [Generate a grounded portfolio summary](photographers/summarize-portfolio/README.md) | `L2-021` |
| photographers | [Link a reference to its photographer](photographers/link-references/README.md) | `L2-022` |
| search | [Search library text with combined filters](search/search-by-keyword/README.md) | `L2-024`, `L2-025` |
| search | [Find inspiration by meaning and related images](search/search-by-meaning/README.md) | `L2-026`, `L2-027` |
| search | [Keep search current as the library changes](search/refresh-search/README.md) | `L2-028` |
| persistence | [Save durable changes without duplicates or lost edits](persistence/save-durable-changes/README.md) | `L2-029`, `L2-030` |
| persistence | [Delete private content and finish cleanup](persistence/delete-content/README.md) | `L2-031`, `L2-032` |
| processing | [Track durable processing and retry failures](processing/track-and-retry-work/README.md) | `L2-033`, `L2-034` |
| processing | [Admit analysis fairly and reuse completed work](processing/admit-analysis/README.md) | `L2-035` |
| processing | [Use explicit demo and live capabilities](processing/select-execution-mode/README.md) | `L2-036` |
| security | [Sign in and access only the owned library](security/access-private-library/README.md) | `L2-037`, `L2-038` |
| security | [Validate content and isolate AI data](security/protect-submitted-content/README.md) | `L2-039`, `L2-041` |
| security | [Fetch only permitted public source content](security/fetch-permitted-source/README.md) | `L2-040` |
| security | [Enforce shared quotas and return safe failures](security/enforce-usage-limits/README.md) | `L2-042` |
| experience | [Navigate the library and recover unfinished edits](experience/navigate-and-recover/README.md) | `L2-043` |
| experience | [Use every workflow across viewports and assistive technology](experience/use-accessible-layouts/README.md) | `L2-044`, `L2-045`, `L2-046` |
| operations | [Browse and process work within measurable budgets](operations/browse-within-budgets/README.md) | `L2-047`, `L2-048` |
| operations | [Diagnose failures and restore the private library](operations/restore-library/README.md) | `L2-049` |
| operations | [Configure, verify, and release Loupe reproducibly](operations/configure-and-release/README.md) | `L2-050` |
| design-system | [Publish the visual reference and apply its tokens](design-system/publish-visual-reference/README.md) | `L2-051`, `L2-052` |

The following deployment and release inputs remain `<TO SUPPLY>`. They are implementation selections or evidence, not missing product requirements.

| Input | Design boundary |
| --- | --- |
| Relational/vector/image storage engines, migrations, and encryption key management | Application ports preserve transaction, ownership, revision, cleanup, and performance contracts |
| OIDC provider and environment callback values | Server-side validated login and opaque cookie session design |
| AI/embedding providers, models, and external retention configuration | Explicit Demo/Live capability adapters and recorded real-provider checks |
| Licensed evaluation assets, frozen semantic corpus, relevance labels, and named reviewers | Fixed numeric release quality gates; no unexecuted check is reported as passing |
| Approved token values, visual baselines, exact browser/package versions, and deployment commands | Independent design-system implementation and reproducible release manifest |
| Backup, independent deletion ledger, telemetry/alert sink, and quota-store adapters | Cross-instance and restore acceptance budgets remain binding |

The design follows primary references for [WCAG 2.2](https://www.w3.org/TR/WCAG22/), [outbound destination protection](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html), and [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery). Loupe's numeric limits come from its own L2 specification.

## Diagrams

Each feature contains context, container, component, and class views plus a sequence for each defined L2 behavior. C4 sources use bundled offline PlantUML macros. Sequence boxes distinguish the Angular frontend, API, Application/Domain, and Infrastructure. The local release-evaluation sequence has no frontend because it executes in `Loupe.Evaluation`.

PlantUML sources and rendered PNG siblings sit in each feature's `diagrams/` folder. The feature pages link the PNG files inline. Diagram labels summarize contracts; the Description sections define the state transitions, failure paths, and unresolved boundaries.
