using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;

namespace Loupe.Application.References;

public static class ReferenceListCursor
{
    public static string Scope(string ownerId, Guid? boardId, int pageSize) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { ownerId, boardId, pageSize })));

    public static string Encode(CreatedCursor position, string scope) => scope + "." + position.Encode();

    public static CreatedCursor? Parse(string? value, string scope)
    {
        if (value is null) return null;
        if (value.Length > 200 || !value.StartsWith(scope + ".", StringComparison.Ordinal))
            throw new RequestValidationException("cursor", "This cursor belongs to a different view. Refresh the list.");
        return CreatedCursor.Parse(value[(scope.Length + 1)..]);
    }
}
