namespace Loupe.Application.Common;

public sealed class RequestValidationException(string field, string message) : Exception("The request contains invalid fields.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]> { [field] = [message] };
}
