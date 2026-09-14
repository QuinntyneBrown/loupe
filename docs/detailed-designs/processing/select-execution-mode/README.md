# Use real providers and archive historical samples

The implemented critique and URL-import configuration accepts `Live` or an
unconfigured capability. Demo execution is rejected at startup. Composition binds
`ICritiqueProvider` to `AzureOpenAiCritiqueProvider`; controlled providers exist only
in acceptance tests. Missing Azure endpoint, deployment, or API key prevents admission without fabricating
results, while manual library work remains available.

The adapter uses Azure's v1 Responses endpoint with server-side API-key
credentials and an explicit deployment name. Stored model/version remains
provenance; `azure-critique-v2` separates new requests from old direct-OpenAI
completed-result reuse. Scouting reports use the same endpoint, credentials,
and deployment through `AzureOpenAiScoutingProvider` with prompt version
`location-scouting-v1` ([produce-scouting-report](../../scouting/produce-scouting-report/README.md)).
Drain work before changing provider/deployment settings.
No persistence migration is needed for this provider change.

`ArchiveDemoCritiques` moves saved sample JSON into a private archive on its
photograph, cancels pending Demo operations and invalidates leases, and clears
current sample operation pointers. It preserves notes, briefs, media, and
historical provenance. Samples no longer mark critiques ready or qualify for
comparison. The owner explicitly requests a replacement; there is no migration
triggered provider charge. Normal photograph deletion also removes the archive.
The legacy enum value remains stable for stored history only.

The photo API exposes an archive flag, not archived critique content. The detail
screen explains the archive and offers the standard real-critique action.
A successful real critique becomes current without overwriting the archive.

See [L2-036 acceptance criteria](../../../specs/L2.md#l2-036-use-real-integrations-and-archive-historical-samples)
and [runtime setup and migration](../../../../backend/README.md).
The diagrams in this directory predate retirement and are historical proposals,
not the implemented provider composition. Other planned AI capabilities must
follow the same real-only configuration policy when implemented.
