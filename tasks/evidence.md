# Acceptance evidence

Record the command, observed red failure, green result, and review for each slice.
No application behavior was implemented before this log was created.

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
