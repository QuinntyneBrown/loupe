# Loupe API

Run the complete API acceptance suite from PowerShell with `./backend/Test.ps1`.
Docker Desktop must be running with Linux containers. The test image starts
isolated PostgreSQL containers through Testcontainers; the Docker socket is
mounted only in this acceptance environment. No production account or AI
credentials are used. Filter a slice with `-Filter FullyQualifiedName~Photographs`.

The acceptance image pins .NET SDK 10.0.400 on Ubuntu 24.04 by image digest and
uses the distribution's libvips and libheif HEVC plugins. The x265 encoder creates
synthetic HEIC acceptance fixtures; libde265 decodes user HEIC uploads. The
prebuilt NetVips.Native package omits HEVC and therefore is not the deployment
runtime. All image acceptance checks must run in the container, including from
Windows. The repository's local .NET 10 SDK can still build and format the code.

`dotnet format backend/Loupe.slnx --verify-no-changes --no-restore` checks C#
formatting after restore. MediatR remains pinned to 12.5.0.

Runtime configuration requires `ConnectionStrings:Library`, absolute private
`Media:Root`, `Jwt:SigningKey` (base64 encoding of at least 32 cryptographically
random bytes), and explicitly enumerated HTTPS `Browser:AllowedOrigins`.
`Jwt:Issuer` and `Jwt:Audience` default to `Loupe`. Keep the issuer stable: it is
part of the existing ownership-key derivation. Store the signing key in secret
configuration, share it between API instances, and never commit it or generate a
new key on each API startup. Changing the key invalidates all issued JWTs.

## Local accounts and authentication

Loupe stores administrator-provisioned users in PostgreSQL and signs its own JWTs.
Apply migrations before starting the new API. The `LocalUsers` migration deletes
old sessions, preserves existing content without reassignment, and creates the
users table. New accounts start with empty libraries. Do not run the old API and
new API together during cutover. Back up the database, stop old API instances,
apply migrations, configure the shared signing secret, provision accounts, and
start the new API instances. Every user must sign in again. A rollback requires
coordinating the matching application/database backup; it does not restore old
sessions or migrate new accounts to external identities.

Build/publish `backend/src/Loupe.Admin` with the API. Configure its
`ConnectionStrings__Library` through secret configuration. It requires neither
JWT keys nor AI settings. Run:

```text
dotnet Loupe.Admin.dll create-user photographer@example.com "Photographer Name"
dotnet Loupe.Admin.dll reset-password photographer@example.com
```

Both commands prompt twice with input hidden when interactive, or read one password
line from stdin when redirected. Never pass passwords as command arguments.
Passwords contain 15–128 Unicode scalar values and are not trimmed. Email addresses
are trimmed and matched case-insensitively. Duplicate creation fails, including
concurrent requests. Password reset preserves the user ID and library, and revokes
all sessions. Credentials are salted and hashed using ASP.NET Core PasswordHasher.
There is no public registration, password-reset endpoint, or account-deletion flow.

The browser first calls `GET /api/session/csrf`, then sends
`POST /api/session/sign-in` with JSON `{ "email": "...", "password": "..." }`,
the `X-CSRF-Token` header and a trusted browser Origin. Successful sign-in returns
`{ "subject": "...", "name": "..." }` and sets `__Host-loupe-session` to a JWT in
a Secure, HttpOnly, SameSite=Lax cookie. JWTs are never returned in JSON or kept in
browser local/session storage. `GET /api/session` refreshes the identity-bound
antiforgery token; use it for subsequent mutations and `POST /api/session/sign-out`.
Private media uses the same cookie, so image elements continue to work.

JWT validation enforces the configured issuer, audience, HS256 signature,
lifetime, subject and session identifier. The database additionally enforces
30-minute idle and exact 12-hour absolute expiry, user password version, and
immediate session revocation. Login replaces the previous session. Sign-out
returns 204 after revocation and cookie removal. Database errors fail closed.
Failed credentials return generic 401; malformed input returns 400; failed
browser protection returns 403. Sign-in permits ten protected attempts per client
IP per minute **per API process**, then returns 429 with `Retry-After`. The current
host does not trust forwarded IP headers; reverse proxies therefore share their
upstream address's budget. Multi-instance public ingress must also enforce an
aggregate sign-in limit and supply a reviewed trusted-proxy configuration if
individual client IP budgets are required.

For a release smoke check, provision a temporary account, sign in through the
production Angular composition over HTTPS, open My Work, sign out, and verify
that a copied prior cookie receives 401. Repeat after a CLI password reset and
verify that only the replacement password works. Acceptance tests use real local
accounts and real persistence; no external identity service is required.

Current behavior and remaining work are recorded in `../tasks/evidence.md` and
`../tasks/todo.md`.

`POST /api/photographs` requires a client-generated `Idempotency-Key` header of
1–128 visible ASCII characters, in addition to the authenticated session and
antiforgery proof. Generate a new key for a new upload and retain it when retrying
the same upload. Normalized title/brief fields, declared format and the byte
digest identify its payload. A key is scoped to its owner and operation for 24
hours; conflicting content returns 409. A retained receipt for an unavailable
photograph returns 404 rather than recreating it.

Photograph deletion uses `DELETE /api/photographs/{id}?revision={revision}`.
A 200 response acknowledges revoked access and returns a deletion operation;
`Pending` means physical cleanup has not completed. Owners can read
`GET /api/deletions/{id}` or repeat the deletion to resolve its current status.

Run `dotnet run --project backend/src/Loupe.Worker` as a separate process after
applying migrations. It uses the same `ConnectionStrings:Library` and private
`Media:Root` as the API, with no JWT or browser configuration. Supply secrets
through deployment configuration. `Cleanup:PollInterval` defaults to one minute
and must be positive and at most five minutes. Multiple worker instances can
share the store. Failed media removal stays pending for subsequent iterations;
completed manifests retain only the deletion identity and timestamps. Journal
retention, orphan scanning and backup restore are subsequent increments.

The worker also scans managed media for abandoned files. Final and `.partial`
files at least one hour old are eligible when no saved photograph references
them. It removes up to 100 per iteration, excludes unknown names and reparse
points, and coordinates with active upload transactions before taking its
reference snapshot. Framework multipart temporary storage is a separate private
staging mount in the deployment work.

Completed deletion records are pruned after 35 days. Pending cleanup records are
retained until their files are removed, even if that takes longer. Repeating a
delete after its record expires returns the normal unavailable response.

Only `Ai:Mode=Live` is supported. Omit the mode to disable AI processing;
missing credentials return `integration_not_configured`, never sample output.
`Imports:Mode=Live` independently enables real URL retrieval.
`Ai:MaxConcurrentCalls` defaults to four and accepts 1–64. Set the same value on
every API/worker instance in a deployment. Each worker can fill that capacity;
shared database leases enforce the deployment total and two calls per owner,
with round-robin selection of eligible owners. Stop workers before lowering the
cap so existing calls drain before the new limit applies.

### Azure OpenAI critiques

Configure the same values on the API and every worker through environment variables
(or the equivalent Microsoft Configuration keys):

```text
Ai__Mode=Live
Ai__Endpoint=https://YOUR-RESOURCE-NAME.openai.azure.com
Ai__Deployment=YOUR-CRITIQUE-DEPLOYMENT
Ai__Model=gpt-5.4-mini-2026-03-17
```

Supply `Ai__ApiKey` separately from your secret store or process environment; never
commit it or place it in frontend configuration. `Ai:Deployment` is the Azure
**deployment name**, not a model identifier. `Ai:Model` is the model/version recorded
on saved critiques (default shown above); it must match the model/version behind
that deployment. Use a deployment supporting image input and strict structured
output through the Responses API. Availability depends on model and region.

`Ai:Endpoint` is the HTTPS resource root, with an optional trailing slash. Do not
include `/openai/v1`, credentials, query strings, or fragments. Invalid supplied
endpoints fail startup with a safe setting-specific error. A missing endpoint,
deployment, or API key disables critique admission and processing while manual
library work remains available. Omit `Ai:Mode` to disable critiques explicitly.
There is no direct-OpenAI fallback or Entra ID authentication in this integration.

The worker posts to `{endpoint}/openai/v1/responses` with the `api-key` header and
the deployment name in `model`. It sends the admitted private JPEG preview, brief,
and allowlisted EXIF; personal notes and storage keys are excluded. It requests
strict structured output with `store=false`. Responses are bounded to two million
bytes, with a 120-second attempt deadline and the existing durable retry policy.
HTTP redirects and client request logging remain disabled. See Microsoft's
[Azure Responses API documentation](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/responses).

`store=false` is not a promise of zero retention across Azure's service. Review
the account's abuse-monitoring configuration and deployment geography/type against
[Microsoft's data-handling documentation](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy)
before submitting private photographs. Global and DataZone deployments have
different processing-location rules from regional deployments.

#### Switching from direct OpenAI

Pause new critique submissions and let queued/running critique operations settle
under the old configuration before stopping API and worker processes. Deploy the
new code and Azure settings together on every instance, then resume submissions.
Repeat this drain procedure before changing endpoint, deployment, or model/version;
in-flight work must not execute against a different deployment than intended.

No database migration is needed for this change. Existing critiques and provenance
remain readable. New Azure requests use `azure-critique-v2`, so completed direct-
OpenAI results are not reused. Existing critiques remain current until a successful
replacement. Retrying a failed direct-OpenAI operation follows the existing
analysis-inputs-changed flow: review the saved brief and request new Azure work.
To roll back, drain Azure work first, then restore the earlier application version
and its direct-OpenAI configuration together; preserve the database and saved results.

#### Live connection check

After supplying real Azure configuration, start the configured API, worker and web
client. Sign in, upload a synthetic or explicitly permitted test photograph, and
request its critique. Verify that admission supplies a durable operation, the worker
completes it, and reopening the photograph shows the structured critique with the
configured model/version. Repeat the same request to check reuse without another
provider call. Record the operation ID, deployed model/version, and observed outcome
without recording secrets, private inputs, or raw provider responses.

For a failure, inspect the safe operation code: credentials/access errors require
account configuration, `provider_disabled` can indicate a missing deployment,
rate limits require capacity or retry, and `invalid_output` requires checking model
support and output quality. Do not describe controlled transport acceptance tests as
a live Azure check or as proof of photographic critique quality.

For the scouting report, create a location, upload two or three synthetic or
explicitly permitted images, set a brief without logistics detail, and request a
scouting report (`POST /api/locations/{id}/scouting-report`). Verify the durable
operation, that the worker completes it, and that the detail shows the six sections
with the configured model and `location-scouting-v1`. Repeat the request to check
reuse without a provider call, then Regenerate to check a new job. Record the same
safe details as above. The release evaluation in `docs/evaluation/scouting/` is a
separate, reviewer-run procedure; a connection check is not evidence of report
quality.

### Local embeddings for Find a location

Meaning search over locations and the search index worker use a local
[Ollama](https://ollama.com) instance beside the API, so notes and briefs never
reach an external provider:

```
Embeddings__Endpoint=http://ollama:11434
Embeddings__Model=bge-m3
```

`Embeddings:Model` defaults to `bge-m3` (1024 dimensions); pull it on the Ollama
host (`ollama pull bge-m3`) before starting the worker. Vectors live in the
PostgreSQL `search_vectors` table, created by the `SearchVectors` migration with
`CREATE EXTENSION IF NOT EXISTS vector`, so the database must ship the pgvector
extension (the `pgvector/pgvector` images do). Without an endpoint the API reports
each location's search status as not configured, keyword search is unaffected, index
intents are still recorded, and the worker stays idle until the endpoint is set.
Vectors carry the model identity they were built with; changing `Embeddings:Model`
means every location re-indexes on its next save, and Meaning cursors minted under
the previous model are refused.

Connection check: with the endpoint configured, save a location and watch its
`indexStatus` move from `updating` to `current` (`GET /api/locations/{id}`); an
`Embeddings:Endpoint` that is unreachable shows `failed` with a retryable
operation.

### Retiring historical samples

Stop API/worker processes before applying `20260910000000_ArchiveDemoCritiques`,
then restart with Live configuration. The migration moves current sample JSON
into the photograph's private `ArchivedDemoCritiqueJson`, preserves its original
provenance and historical operation output, clears sample operation pointers,
and cancels queued/running Demo work and leases. Live critiques are unchanged.
Photos, briefs, notes, and media remain intact; comparison eligibility uses only
current critiques. No replacement analysis is automatically submitted. The photo
screen explains the archive and offers a real request. Deleting a photograph
also deletes its archive under the normal deletion policy.

The migration's rollback restores an archived sample only if no newer critique
exists; canceled jobs are not restarted. Back up the database before deployment.
`ExecutionMode.Demo = 0` remains solely for persisted historical compatibility.
Test fixtures use injected controlled providers with Live provenance, never a
production simulation adapter.

Evidence coordinates use normalized positions on the oriented analysis image.
The strict provider schema requires a nullable region; uncertain or global
statements use null. Existing critiques without coordinates remain readable.
