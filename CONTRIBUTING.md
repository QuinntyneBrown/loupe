# Contributing to Loupe

Thank you for your interest in improving Loupe. This document explains how the
project is developed and what a good contribution looks like. The conventions
below are not optional; they are what keep the codebase small, testable and safe
to change.

## Before you start

- Read [`AGENTS.md`](AGENTS.md). It is the single source of engineering guidance
  and applies to people and coding agents alike.
- Check [`docs/specs/L2.md`](docs/specs/L2.md) and [`tasks/todo.md`](tasks/todo.md)
  for the numbered acceptance criteria. Work is organised around those criteria;
  a change that does not map to one usually needs a requirement first.
- For anything larger than a bug fix, open an issue describing the behaviour you
  intend to deliver so the scope can be agreed before code is written.

## Development workflow

Every production change follows the same incremental, acceptance-test-driven
cycle:

1. **Pick one criterion-level behaviour.** Plan it as small, reviewable slices.
2. **Write the acceptance criteria** for the slice as Given–When–Then.
3. **Write the acceptance test first** — a backend integration test against the
   API, or a Playwright test using a page object — and run it to prove it fails
   for the expected reason.
4. **Implement only what makes that test pass**, then refactor with the tests green.
5. **Run the relevant regression checks** (see below) before moving to the next slice.

No bulk implementation, no tests added afterwards, and never weaken a test to
manufacture a pass. Finish the whole behaviour — edge cases and error paths
included — rather than quietly narrowing scope.

## Conventions the codebase enforces

- **Backend:** Clean Architecture with dependencies pointing inward; controllers
  bind, dispatch through MediatR and return; commands, queries, handlers and
  validators live in `Loupe.Application`; one type per file, named for the type;
  folders and namespaces agree. MediatR stays pinned to **12.5.0**.
- **Frontend:** signals for state, RxJS only for genuine streams; no single-file
  components; every service is consumed through an interface plus an
  `InjectionToken` declared in `<entity>.service.contract.ts`; components are
  placed by what they know (`components` → `domain` → application).
- **Design tokens:** component stylesheets read `var(--lp-*)` tokens owned by
  `design-system/`. A hard-coded colour, dimension or font stack is a defect —
  add the token first.
- **Tests prove behaviour, never structure.** Do not add naming, layout,
  banned-API or traceability tests. Frontend tests run in Chromium only and put
  every selector in a page object.

## Running the checks

```powershell
dotnet build backend/Loupe.slnx
dotnet format backend/Loupe.slnx --verify-no-changes --no-restore
./backend/Test.ps1                       # integration tests in the acceptance container
cd frontend && npm ci && npm run build && npm test
cd ../design-system && npm ci && npm test
```

Record what you ran and what you observed (RED → GREEN) in the pull request; the
project keeps that evidence in [`tasks/evidence.md`](tasks/evidence.md).

## Commits and pull requests

- Use focused commits with conventional messages, e.g.
  `feat(search): query private library keywords with combined filters and paging`
  or `fix(references): refresh links after uncertain inline saves`.
- Keep pull requests scoped to one behaviour. Describe the criterion, the tests
  added, and anything deliberately left out.
- Do not commit secrets, real credentials, or data belonging to real people.
  Demo accounts and fixtures must be obviously synthetic.
- Pull requests are reviewed for correctness, readability, architecture,
  security and performance. Expect questions about tests before questions about
  code.

## Documentation

If a change alters public behaviour, configuration, or a workflow, update the
relevant guide (`backend/README.md`, `frontend/README.md`,
`design-system/README.md`, `docs/`) in the same pull request.

## Code of conduct

Participation in this project is governed by the
[Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).
