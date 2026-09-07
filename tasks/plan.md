# Loupe implementation

Implement every numbered criterion in docs/specs/L2.md, using docs/detailed-designs
and AGENTS.md. Existing mockups are visual seeds, not behavioral specifications.

## Decisions

- .NET 10, MediatR 12.5.0, Angular 22, Node 24.18.0; lock dependencies.
- PostgreSQL, EF Core/Npgsql, pgvector; transactional outbox, sessions, quotas,
  operation keys, revisions, and owned relationships.
- Local Docker Compose; Keycloak OIDC; secure opaque application sessions.
- Private mounted media, NetVips/libheif; authorized image delivery.
- Backend image acceptance and deployment use Linux system libvips/libheif with
  HEVC support; NetVips.Native's prebuilt runtime omits that codec. The acceptance
  container pins .NET SDK 10.0.400/Ubuntu 24.04; local builds use the installed
  .NET 10 SDK. Test commands and exact native packages are in backend/.
- OpenAI GPT-5.4 mini for critique, metadata, summaries; Ollama bge-m3 for local
  embeddings. Explicit deterministic Demo and configured Live modes.
- Independent static design-system site; authoritative --lp- visual tokens.
- HTTPS, externally supplied secrets, encrypted storage/backups, independently
  retained deletion ledger. Hourly backups, deletion replay before readiness.

## Delivery order

1. Independent accessible design-system primitive, then remaining examples.
2. Identity/private library, durable upload, brief, notes, browsing, deletion.
3. Durable critique request/status/failure/retry/replacement/reuse, comparison.
4. Uploaded/link-only references, safe import/fallback, editing/replacement.
5. Suggested metadata generation/review and manual metadata preservation.
6. Boards, memberships, rename and deletion.
7. Photographer bookmarks, grounded summaries, reference associations.
8. Keyword matching, combined filters, pagination and navigable search state.
9. Local semantic indexing, meaning search, related items, generation rebuild.
10. Complete cleanup, restart/scale recovery, migrations and backup restore.
11. Capacity, real-provider quality, accessibility/mobile/visual review, release.

## Mandatory increment cycle

Select the next criterion-level behavior from todo.md. Write and run the API
integration or Playwright acceptance test before production behavior. Record the
expected missing-behavior failure in evidence.md. Implement that behavior only,
refactor green, run applicable regression/build checks, review, and commit.
Security, accessibility, persistence, deletion, and diagnostics apply as soon as
their feature exists. Checkpoints do not reduce the scope of later criteria.

Backend tests use real persistence and controlled external boundaries. Frontend
tests use injected service mocks and screen page objects owning all selectors.
No architecture tests or specification-parsing tests. Each acceptance test names
its L2 criterion. Current automated evidence is separate from real-service smoke
checks and named human reviews. Never report an unexecuted check as passing.

## Release prerequisites

All criteria, performance thresholds and quality gates remain binding. Record
exact runtime/model/browser versions, licensed or synthetic fixtures, frozen
relevance labels, reviewer judgments, deployment and restore commands. Missing
credentials, named reviews or device access prevent the affected release gate
from being marked complete; they do not prevent independent implementation.
