using Loupe.Application.Common;

namespace Loupe.Application.ReferenceDrafts;

public static class DraftOperationKey
{
    public static string Validate(string? value) => value is { Length: > 0 and <= 128 } key && key.All(character => character is >= '!' and <= '~')
        ? key : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
}
