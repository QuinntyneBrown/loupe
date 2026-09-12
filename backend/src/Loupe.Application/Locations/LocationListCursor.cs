using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;

namespace Loupe.Application.Locations;

public static class LocationListCursor
{
    public static string Scope(string ownerId, int pageSize) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { ownerId, pageSize })));

    public static string Encode(CreatedCursor position, string scope) => scope + "." + position.Encode();

    public static CreatedCursor? Parse(string? value, string scope)
    {
        if (value is null) return null;
        if (value.Length > 200 || !value.StartsWith(scope + ".", StringComparison.Ordinal))
            throw new RequestValidationException("cursor", "This cursor belongs to a different view. Refresh the list.");
        return CreatedCursor.Parse(value[(scope.Length + 1)..]);
    }
}
