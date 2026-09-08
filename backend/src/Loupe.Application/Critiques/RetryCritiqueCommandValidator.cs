using Loupe.Application.Common;

namespace Loupe.Application.Critiques;

public static class RetryCritiqueCommandValidator
{
    public static string Validate(RetryCritiqueCommand request)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Provide the photograph revision.");
        return request.OperationKey is { Length: > 0 and <= 128 } key && key.All(character => character is >= '!' and <= '~')
            ? key : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
    }
}
