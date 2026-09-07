# Loupe backend

Use the .NET SDK selected by the root `global.json`.

```powershell
dotnet test backend/Loupe.slnx
dotnet format backend/Loupe.slnx --verify-no-changes --no-restore
dotnet run --project backend/src/Loupe.Api
```

The current increment establishes the protected session-read boundary. Sign-in,
durable sessions and library operations are tracked in `../tasks/todo.md`.
MediatR remains pinned to 12.5.0. Each acceptance test identifies its L2 coverage.

API startup requires Identity:Authority (HTTPS), Identity:ClientId, and Identity:ClientSecret via Microsoft Configuration (environment variables use double underscores). Never put credentials in tracked configuration. API acceptance uses a controlled identity fixture and requires no real provider.
