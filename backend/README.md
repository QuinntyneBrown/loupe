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
