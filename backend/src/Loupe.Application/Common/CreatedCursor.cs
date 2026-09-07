using System.Globalization;
using System.Text;

namespace Loupe.Application.Common;

public sealed record CreatedCursor(DateTimeOffset CreatedAt, Guid Id)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{CreatedAt.UtcTicks}:{Id:N}")));

    public static CreatedCursor? Parse(string? value)
    {
        if (value is null) return null;
        try
        {
            if (value.Length > 128) throw new FormatException();
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out var id)) throw new FormatException();
            return new CreatedCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        { throw new RequestValidationException("cursor", "This continuation cursor is invalid. Refresh the list."); }
    }
}
