using Loupe.Application.Common;

namespace Loupe.Application.Sessions;

public static class BeginSignInQueryValidator
{
    public static void Validate(string returnUrl)
    {
        var decoded = Uri.UnescapeDataString(returnUrl);
        if (returnUrl.EnumerateRunes().Count() > 2048 || !decoded.StartsWith('/') ||
            decoded.StartsWith("//", StringComparison.Ordinal) || decoded.Contains('\\') || decoded.Any(char.IsControl))
        {
            throw new RequestValidationException("returnUrl", "Choose a local application destination.");
        }
    }
}
