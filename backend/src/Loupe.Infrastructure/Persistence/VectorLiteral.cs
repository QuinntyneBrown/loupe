using System.Globalization;

namespace Loupe.Infrastructure.Persistence;

public static class VectorLiteral
{
    /// <summary>Formats a vector as pgvector's text input form, "[x1,x2,...]".</summary>
    public static string Format(float[] vector) => "[" + string.Join(',', vector.Select(value => value.ToString("R", CultureInfo.InvariantCulture))) + "]";
}
