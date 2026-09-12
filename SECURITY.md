# Security Policy

Loupe stores private photographs, personal notes and authentication state, so we
take reports about its security seriously and appreciate responsible disclosure.

## Supported versions

Loupe is under active development on the `main` branch. Security fixes are made
on `main`; there are no maintained release branches yet.

| Branch | Supported |
| --- | --- |
| `main` | Yes |
| anything else | No |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues,
discussions or pull requests.**

Report privately through GitHub's vulnerability reporting for this repository:

<https://github.com/QuinntyneBrown/loupe/security/advisories/new>

Include as much of the following as you can:

- The type of issue (for example: authentication bypass, SSRF in URL import,
  cross-tenant data access, unsafe media handling, injection).
- The affected area — API route, worker, Angular component, design-system page —
  and the commit or branch you tested against.
- Step-by-step instructions to reproduce, including any proof-of-concept.
- The impact you believe the issue has, and any suggested mitigation.

Please report in English if possible.

## What to expect

- An acknowledgement within **5 business days**.
- A triage decision and, where confirmed, a remediation plan communicated through
  the advisory thread. We aim to fix confirmed high-severity issues within
  **30 days**.
- Credit in the advisory once a fix is published, unless you prefer to remain
  anonymous.

We ask that you give us reasonable time to address the issue before any public
disclosure, and that you avoid accessing, modifying or deleting data that is not
your own while researching.

## Scope notes for researchers

- Demo accounts (`photographer@example.com`, `api-demo@example.com`) and the
  synthetic password used by the recording harness are intentionally public and
  only ever exist in disposable local stacks; they are not a finding.
- The application's threat model expects: private-by-owner libraries, sign-in
  rate limiting per API process, CSRF and trusted-origin checks on every
  mutation, SSRF-checked outbound fetches for URL imports, and private media
  delivery. Anything that lets one user read or change another user's data, or
  lets the server reach internal addresses, is in scope.
- Secrets are supplied through deployment configuration; a secret committed to
  this repository would be a finding.
