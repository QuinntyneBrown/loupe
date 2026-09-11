# Loupe web client

Requires Node 24.18+ and npm. From this directory, run `npm ci` and `npm run build`.
The build mirrors the authoritative design-system tokens, builds the three Angular
libraries, and emits the production application to `dist/loupe/browser`.

`npm start` serves the production HTTP composition during development after the
libraries have been built. API and identity callback routes must be served through
the same HTTPS origin as the application. Deployment configuration is documented
with the container stack when that increment is delivered.

For deterministic browser acceptance, run `npm ci` in `../e2e`, then `npm test`
here. Playwright starts its own separate mock composition on port 4207. It does
not contact the production HTTP adapter or the real account database.

The `api` library owns contracts and adapters. `components` is reserved for
publishable presentation primitives, and `domain` for components consuming API
contracts. Routed pages and application composition live in `projects/loupe`.
These are separate Angular projects with independent library build targets.

See `../tasks/evidence.md` for delivered behavior and test evidence. The complete
product scope remains in `../tasks/todo.md`; this is an incremental implementation.

Sign-in uses local email/password credentials through `ISessionService` and
`SESSION_SERVICE`. The production adapter obtains anonymous antiforgery proof,
posts credentials, then refreshes authenticated session/antiforgery state. The
browser retains only an HttpOnly session cookie. Playwright replaces this adapter
with the fixture implementation; its account is `photographer@example.com` with
the synthetic password `local acceptance password`.
