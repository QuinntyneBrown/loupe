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
