# Loupe API

Run the complete API acceptance suite from PowerShell with `./backend/Test.ps1`.
Docker Desktop must be running with Linux containers. The test image starts
isolated PostgreSQL containers through Testcontainers; the Docker socket is
mounted only in this acceptance environment. No production identity or AI
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
`Media:Root`, `Identity:Authority` (HTTPS), `Identity:ClientId`,
`Identity:ClientSecret`, and explicitly enumerated HTTPS
`Browser:AllowedOrigins`. Browser/API/identity deployment and secret provisioning
are delivered with the Compose increment. Current behavior and remaining work
are recorded in `../tasks/evidence.md` and `../tasks/todo.md`.

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
`Media:Root` as the API, with no browser identity configuration. Supply secrets
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
