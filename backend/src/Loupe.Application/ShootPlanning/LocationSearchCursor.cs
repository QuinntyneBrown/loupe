using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;

namespace Loupe.Application.ShootPlanning;

public static class LocationSearchCursor
{
    public static string Scope(string ownerId, string mode, LocationSearchFilter filter, int pageSize) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { ownerId, mode, filter, pageSize }))) + ".";

    public static string Encode(CreatedCursor position, string scope) => scope + position.Encode();

    public static CreatedCursor? Parse(string? value, string scope)
    {
        if (value is null) return null;
        if (value.Length > 200 || !value.StartsWith(scope, StringComparison.Ordinal))
            throw new RequestValidationException("cursor", "This cursor belongs to a different search. Refresh the results.");
        return CreatedCursor.Parse(value[scope.Length..]);
    }
}
