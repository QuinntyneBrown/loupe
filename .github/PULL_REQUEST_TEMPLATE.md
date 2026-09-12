## Summary

What behaviour this change delivers, and which acceptance criterion it maps to
(for example `L2-026.4`).

## Acceptance tests

- Given–When–Then for the slice:
- Test(s) added or changed:
- Observed RED before implementation, GREEN after:

## Checks run

- [ ] `dotnet build backend/Loupe.slnx` and `dotnet format … --verify-no-changes`
- [ ] `./backend/Test.ps1` (or the relevant `-Filter`)
- [ ] `npm run build` and `npm test` in `frontend/`
- [ ] `npm test` in `design-system/` (if tokens or components changed)

## Notes for reviewers

Anything deliberately left out, follow-ups, or documentation updated.
